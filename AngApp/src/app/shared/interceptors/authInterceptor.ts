import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from "@angular/common/http";
import { inject } from "@angular/core";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { catchError, Observable } from "rxjs";

export function authInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
    const oidcSecurityService = inject(OidcSecurityService);

    let cloned:HttpRequest<unknown>|null = null;
    
    if(oidcSecurityService.authenticated().isAuthenticated){
        oidcSecurityService.getAccessToken().subscribe(token=>{
            cloned = req.clone({setHeaders: {Authorization: "Bearer " + token}});
            //return next(cloned);// wrong, doesn't work.
        });
    }
    else{
        cloned = null;
    }

    if(cloned){
        return next(cloned);
    }
    return next(req);
}