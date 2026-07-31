import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';

import { provideAuth, LogLevel } from 'angular-auth-oidc-client';
import { authInterceptor } from './shared/interceptors/authInterceptor';
import { MyErrorHandler } from './shared/my-error-handler';

export const appConfig: ApplicationConfig = {
  providers: [
    //provideBrowserGlobalErrorListeners(),
    {provide: ErrorHandler, useClass: MyErrorHandler},
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([authInterceptor]),
      withXsrfConfiguration({
        cookieName: "XSRF-TOKEN",
        headerName: "X-CSRF-TOKEN"
      }),
    ),
    provideAuth({
      config: {
        authority: 'https://localhost:5443',
        redirectUrl: "https://localhost:5445",
        postLogoutRedirectUri: "https://localhost:5445",
        clientId: 'UserProfileApp',
        scope: 'openid email roles offline_access',
        responseType: 'code',
        silentRenew: true,
        useRefreshToken: true,
        logLevel: LogLevel.Debug,
      },
    }),
  ]
};
