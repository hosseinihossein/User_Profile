import { Component, inject, signal } from '@angular/core';
import { form, FormField, maxLength, minLength, required } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogClose, MatDialogContent } from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';

@Component({
  selector: 'app-textarea-dialog',
  imports: [MatDialogContent, MatFormField, MatLabel, MatInput, MatError, FormField, MatDialogActions, 
    MatButton, MatDialogClose],
  templateUrl: './textarea-dialog.html',
  styleUrl: './textarea-dialog.css',
})
export class TextareaDialog {
  //readonly dialogRef = inject(MatDialogRef<TextareaDialog>);
  readonly data = inject<TextareaDialogModel>(MAT_DIALOG_DATA);
  
  inputModel = signal({myInput:this.data.value});
  inputForm = form(this.inputModel,(schemaPath) => {
    required(schemaPath.myInput, {message: `${this.data.name} is required!`});
    
    if(this.data.minLength){
      minLength(schemaPath.myInput, this.data.minLength, {message:`${this.data.name} must be at least ${this.data.minLength} characters`});
    }
    if(this.data.maxLength){
      maxLength(schemaPath.myInput, this.data.maxLength, {message:`${this.data.name} can not be more than ${this.data.maxLength} characters`});
    }
  });
}

export class TextareaDialogModel {
  name:string = "This field";
  value:string = "";
  minLength?:number;
  maxLength?:number;
  canDelete:boolean = false;
}
