import { Component, inject, signal } from '@angular/core';
import { debounce, email, form, FormField, maxLength, minLength, required, validateHttp } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogRef } from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';

@Component({
  selector: 'app-email-input-dialog',
  imports: [MatDialogContent, MatFormField, MatLabel, MatInput, MatError, FormField, MatDialogActions, 
    MatButton, MatDialogClose, ],
  templateUrl: './email-input-dialog.html',
  styleUrl: './email-input-dialog.css',
})
export class EmailInputDialog {
  //readonly dialogRef = inject(MatDialogRef<EmailInputDialog>);
  readonly data = inject<EmailInputDialogModel>(MAT_DIALOG_DATA);
  
  inputModel = signal({myInput:this.data.value});
  inputForm = form(this.inputModel,(schemaPath) => {
    required(schemaPath.myInput, {message: `${this.data.name} is required!`});
    
    if(this.data.minLength){
      minLength(schemaPath.myInput, this.data.minLength, {message:`${this.data.name} must be at least ${this.data.minLength} characters`});
    }
    if(this.data.maxLength){
      maxLength(schemaPath.myInput, this.data.maxLength, {message:`${this.data.name} can not be more than ${this.data.maxLength} characters`});
    }

    if(this.data.email){
      email(schemaPath.myInput, {message: 'Please enter a valid email address'})
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

export class EmailInputDialogModel {
  name:string = "This field";
  value:string = "";
  email?:boolean = false;
  minLength?:number;
  maxLength?:number;
  httpValidationAddress?:string;
  httpInvalidityMessage?:string;
  canDelete:boolean = false;
}
