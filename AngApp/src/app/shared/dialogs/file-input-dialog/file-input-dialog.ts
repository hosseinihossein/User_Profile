import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { form, maxLength, minLength, required, FormField } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogContent, MatDialogActions, MatDialogClose } from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from "@angular/material/form-field";
import { MatInput } from '@angular/material/input';

@Component({
  selector: 'app-file-input-dialog',
  imports: [MatDialogContent, MatFormField, MatLabel, FormField, MatError, MatDialogActions,
    MatDialogClose, MatButton, MatInput
  ],
  templateUrl: './file-input-dialog.html',
  styleUrl: './file-input-dialog.css',
})
export class FileInputDialog {
  //readonly dialogRef = inject(MatDialogRef<FileInputDialog>);
  readonly data = inject<FileInputDialogModel>(MAT_DIALOG_DATA);

  selectedFile = signal<File | null>(null);
  previewImgSrc = signal(this.data.value);

  canvas = viewChild<ElementRef<HTMLCanvasElement>>("canvas");

  inputModel = signal({title:this.data.title, caption: this.data.caption});
  inputForm = form(this.inputModel,(schemaPath)=>{
    if(this.data.hasTitle){
      required(schemaPath.title,{message:"title is required"});
    }
    if(this.data.hasCaption){
      required(schemaPath.caption,{message:"caption is required"});
    }

    maxLength(schemaPath.title, 128, {message: "title can not be more than 128 characters"});
    maxLength(schemaPath.caption, 1024, {message: "title can not be more than 1024 characters"});
  });

  onSelectFile(event:Event){
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      if(this.data.type === "image"){
        if(this.data.maxSize && (input.files[0].size > this.data.maxSize)){
          if(this.canvas()){
            const reader = new FileReader(); // Create a FileReader instance
  
            // Load the image as a Data URL
            reader.onload = (e)=> {
              const img = new Image();
              img.onload = ()=>{
                const ctx = this.canvas()!.nativeElement.getContext("2d");
                // Set new size
                const newWidth = 800;
                const newHeight = Math.round((img.height / img.width) * newWidth);
  
                this.canvas()!.nativeElement.width = newWidth;
                this.canvas()!.nativeElement.height = newHeight;
  
                // Draw resized image
                if(ctx){
                  ctx.drawImage(img, 0, 0, newWidth, newHeight);
                }
  
                // Convert to data URL (can be uploaded or displayed)
                const resizedDataUrl = this.canvas()!.nativeElement.toDataURL("image/webp", 0.8); // 0.8 = quality only for image/jpeg and image/webp
                this.previewImgSrc.set(resizedDataUrl);
                
                this.canvas()!.nativeElement.toBlob((blob)=>{
                  if(blob){
                    let file = new File([blob], "editedUserImage", {
                      type: blob.type || "application/octet-stream",
                      lastModified: Date.now()
                    });
                    this.selectedFile.set(file);
                  }
                },"image/webp", 0.8);
              };
              img.src = e.target!.result as string ?? this.data.value;
            };
  
            reader.readAsDataURL(input.files[0]); // Read the file as a Data URL
          }
          else{
            /*this.imageSizeError.set(
              `The size of the choosen image cannot be more than ${this.data.maxSize / 1024} KB!`
            );*/
            throw(new Error(`The size of the choosen image cannot be more than ${this.data.maxSize / 1024} KB!`) )
          }
        }
        else{
          this.selectedFile.set(input.files[0]);
  
          const reader = new FileReader(); // Create a FileReader instance
  
          // Load the image as a Data URL
          reader.onload = (e)=> {
            this.previewImgSrc.set(e.target!.result as string ?? this.data.value);
          };
  
          reader.readAsDataURL(input.files[0]); // Read the file as a Data URL
        }
      }
      else/* if(this.data.type === "file")*/{
        this.selectedFile.set(input.files[0]);
      }
    }
    else{
      this.selectedFile.set(null);
      this.previewImgSrc.set(this.data.value);
    }
  }

}

export class FileInputDialogModel {
  type:"image"|"file" = "file";
  value?:string;
  maxSize?:number;
  title:string = "";
  hasTitle:boolean = false;
  caption:string = "";
  hasCaption:boolean = false;
  canDelete?:boolean = false; 
}
