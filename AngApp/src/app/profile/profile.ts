import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { WaitSpinner } from '../shared/wait-spinner/wait-spinner';
import { AsyncPipe, DatePipe, JsonPipe, NgOptimizedImage } from '@angular/common';
import { SingletonService } from '../shared/singleton-service';
import { MatCard, MatCardHeader, MatCardModule } from '@angular/material/card';
import { MatButton, MatIconButton, MatMiniFabButton } from '@angular/material/button';
import { ProfileService } from './profile-service';
import { MatIcon } from '@angular/material/icon';
import { MatTooltip } from '@angular/material/tooltip';
import { RouterLink } from "@angular/router";
import { MatDialog } from '@angular/material/dialog';
import { FileInputDialog, FileInputDialogModel } from '../shared/dialogs/file-input-dialog/file-input-dialog';

@Component({
  selector: 'app-profile',
  imports: [WaitSpinner, DatePipe, MatCardModule, MatButton, NgOptimizedImage, MatIcon, MatTooltip, MatIconButton, MatMiniFabButton],
  templateUrl: './profile.html',
  styleUrl: './profile.css',
})
export class Profile {
  readonly oidcSecurityService = inject(OidcSecurityService);
  private readonly singleton = inject(SingletonService);
  private readonly profileService = inject(ProfileService);
  readonly dialog = inject(MatDialog);

  displayWait = linkedSignal(()=>!this.singleton.authChekced());
  idToken = signal<any>(null);
  profileModel = signal<Profile_Model|null>(null);
  profileImgSrc = computed(()=>this.profileService.getProfileImageAddress(this.profileModel()));
  editMode = signal(false);

  constructor(){
    effect(()=>{
      if(this.oidcSecurityService.authenticated().isAuthenticated){
        this.oidcSecurityService.getPayloadFromIdToken().subscribe(token=>{
          this.idToken.set(token);
        });
      }
      else{
        this.idToken.set(null);
      }
    });
    effect(()=>{
      if(this.idToken() && this.idToken().sub){
        this.profileService.getProfileModel(this.idToken().sub).subscribe({
          next: res => {
            this.profileModel.set(res);
            this.displayWait.set(false);
          },
        });
      }
      else{
        this.profileModel.set(null);
        this.displayWait.set(false);
      }
    });
  }

  login() {
    // User clicked sign-in: redirect to the identity provider.
    this.oidcSecurityService.authorize();
    this.displayWait.set(true);
  }

  logout() {
    this.oidcSecurityService.logoff().subscribe((result) => console.log(result));
    this.displayWait.set(true);
  }

  editProfileImage(){
    if(!this.profileModel()) return;

    this.profileService.getCsrf().subscribe(/*{next: ()=>{console.log("csrf received")},}*/);

    let dialogData = new FileInputDialogModel();
    dialogData.canDelete = true;
    dialogData.hasTitle = false;
    dialogData.hasCaption = false;
    dialogData.type = "image";
    dialogData.value = this.profileImgSrc() ?? undefined;
    dialogData.maxSize = 120 * 1024;

    this.dialog.open(FileInputDialog, { data: dialogData, }).afterClosed().subscribe(result=>{
      if(result === "Delete"){
        this.profileService.deleteUserImage().subscribe({
          next: res => {
            if(res && res.success){
              this.profileModel.update(p=>{
                const newProfileModel = new Profile_Model(p!);
                newProfileModel.hasImage = false;
                newProfileModel.imageVersion = 0;
                newProfileModel.remoteImageUrl = null;
                return newProfileModel;
              });
            }
          },
        });
      }
      else if(result && result.file && result.file instanceof File){
        let userImage = result.file as File;
        this.profileService.postUserImage(userImage).subscribe({
          next: res => {
            if(res && res.success){
              this.profileModel.update(p=>{
                const newProfileModel = new Profile_Model(p!);
                newProfileModel.hasImage = res.hasImage;
                newProfileModel.imageVersion = res.imageVersion;
                return newProfileModel;
              });
            }
          },
        });
      }
    });
  }

}

export class Profile_Model {
  constructor(data?:Partial<Profile_Model>){
    this.id = data?.id ?? "";
    this.username = data?.username ?? "";
    this.email = data?.email ?? "";
    this.emailConfirmed = data?.emailConfirmed ?? false;
    this.displayName = data?.displayName;
    this.description = data?.description;
    this.publicEmail = data?.publicEmail ?? false;
    this.imageVersion = data?.imageVersion ?? 0;
    this.hasImage = data?.hasImage ?? false;
    this.remoteImageUrl = data?.remoteImageUrl;
    this.createdAt = data?.createdAt ?? new Date();
    this.roles = [... data?.roles ?? ""];
  }

  id:string = "";
  username:string = "";
  email:string = "";
  emailConfirmed:boolean = false;
  displayName?:string|null;
  description?:string|null;
  publicEmail:boolean = false;
  imageVersion:number = 0;
  hasImage:boolean = false;
  remoteImageUrl?:string|null;
  createdAt:Date = new Date();
  roles:string[] = [];
}
