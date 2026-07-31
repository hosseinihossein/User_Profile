import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Profile_Model } from './profile';

@Service()
export class ProfileService {
    private readonly httpClient = inject(HttpClient);

    getProfileModel(id:string){
        return this.httpClient.get<Profile_Model>("/User/Profile/Api/ProfileModel");
    }

    getProfileImageAddress(profileModel:{
        id:string, 
        imageVersion:number, 
        hasImage:boolean, 
        remoteImageUrl?:string|null
    }|null):string|null{
        if(profileModel?.hasImage && profileModel.id){
            return `/User/Profile/Api/ProfileImage?id=${profileModel.id}&v=${profileModel.imageVersion}`;
        }
        else if(profileModel?.remoteImageUrl){
            return profileModel.remoteImageUrl;
        }
        return null;
    }

    getCsrf(){
        return this.httpClient.get("/User/Profile/Api/GetCsrf");
    }

    postUserImage(userImage:File){
        let formData = new FormData();
        formData.append("UserImage",userImage);
        return this.httpClient.post<{success:boolean,hasImage:boolean,imageVersion:number}>(
            "/User/Profile/Api/SubmitUserImage", formData
        );
    }
    deleteUserImage(){
        return this.httpClient.delete<{success:boolean}>("/User/Profile/Api/DeleteUserImage");
    }

}
