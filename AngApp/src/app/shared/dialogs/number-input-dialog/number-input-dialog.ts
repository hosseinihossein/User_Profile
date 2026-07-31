import { Component, inject, signal } from '@angular/core';
import { debounce, form, FormField, max, min, required, validateHttp } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogRef } from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';

@Component({
  selector: 'app-number-input-dialog',
  imports: [MatDialogContent, MatFormField, MatLabel, MatInput, MatError, FormField, MatDialogActions, 
    MatButton, MatDialogClose,],
  templateUrl: './number-input-dialog.html',
  styleUrl: './number-input-dialog.css',
})
export class NumberInputDialog {
  //readonly dialogRef = inject(MatDialogRef<NumberInputDialog>);
  readonly data = inject<TextInputDialogModel>(MAT_DIALOG_DATA);
  
  inputModel = signal({myInput:this.data.value});
  inputForm = form(this.inputModel,(schemaPath) => {
    required(schemaPath.myInput, {message: `${this.data.name} is required!`});
    
    if(this.data.min){
      min(schemaPath.myInput, this.data.min, {message:`${this.data.name} must be at least ${this.data.min} characters`});
    }
    if(this.data.max){
      max(schemaPath.myInput, this.data.max, {message:`${this.data.name} can not be more than ${this.data.max} characters`});
    }

    if(this.data.httpValidationAddress){
      debounce(schemaPath.myInput, 1000);
      validateHttp(schemaPath.myInput, {
        request: ({value}) => `${this.data.httpValidationAddress}${value()}`,
        onSuccess: (response: {valid:boolean}) => {
          if (response.valid) {
            return null;
          }
          else {
            return {
              kind: 'serverValidation',
              message: this.data.httpInvalidityMessage || "Invalid by server",
            };
          }
        },
        onError: (error) => ({
          kind: 'networkError',
          message: `Could not verify ${this.data.name} validity by server`,
        }),
      });
    }
  });
  
}

export class TextInputDialogModel {
  name:string = "This field";
  value:number = 0;
  min?:number;
  max?:number;
  httpValidationAddress?:string;
  httpInvalidityMessage?:string;
  canDelete:boolean = false;
}
