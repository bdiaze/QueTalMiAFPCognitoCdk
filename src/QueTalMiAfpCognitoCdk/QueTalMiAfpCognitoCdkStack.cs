using Amazon.CDK;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.SSM;
using Amazon.CDK.Pipelines;
using Constructs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QueTalMiAfpCognitoCdk
{
    public class QueTalMiAfpCognitoCdkStack : Stack
    {
        internal QueTalMiAfpCognitoCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");
            string region = System.Environment.GetEnvironmentVariable("REGION_AWS") ?? throw new ArgumentNullException("REGION_AWS");

            string emailSubject = System.Environment.GetEnvironmentVariable("VERIFICATION_SUBJECT") ?? throw new ArgumentNullException("VERIFICATION_SUBJECT");
            string emailBody = System.Environment.GetEnvironmentVariable("VERIFICATION_BODY") ?? throw new ArgumentNullException("VERIFICATION_BODY");

            string cognitoDomain = System.Environment.GetEnvironmentVariable("COGNITO_DOMAIN") ?? throw new ArgumentNullException("COGNITO_DOMAIN");

            // Se obtienen los clients y secrets para los identity providers...
            // string microsoftClientId = System.Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_ID") ?? throw new ArgumentNullException("MICROSOFT_CLIENT_ID");
            // string microsoftClientSecret = System.Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_SECRET") ?? throw new ArgumentNullException("MICROSOFT_CLIENT_SECRET");

            string googleClientId = System.Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? throw new ArgumentNullException("GOOGLE_CLIENT_ID");
            string googleClientSecret = System.Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? throw new ArgumentNullException("GOOGLE_CLIENT_SECRET");

            // string facebookClientId = System.Environment.GetEnvironmentVariable("FACEBOOK_CLIENT_ID") ?? throw new ArgumentNullException("FACEBOOK_CLIENT_ID");
            // string facebookClientSecret = System.Environment.GetEnvironmentVariable("FACEBOOK_CLIENT_SECRET") ?? throw new ArgumentNullException("FACEBOOK_CLIENT_SECRET");

            string[] callbackUrls = System.Environment.GetEnvironmentVariable("CALLBACK_URLS").Split(",") ?? throw new ArgumentNullException("CALLBACK_URLS");
            string[] logoutUrls = System.Environment.GetEnvironmentVariable("LOGOUT_URLS").Split(",") ?? throw new ArgumentNullException("LOGOUT_URLS");


            UserPool userPool = new(this, $"{appName}UserPool", new UserPoolProps {
                UserPoolName = $"{appName}UserPool",
                SelfSignUpEnabled = true,
                SignInCaseSensitive = false,
                UserVerification = new UserVerificationConfig {
                    EmailSubject = emailSubject,
                    EmailBody = emailBody,
                    EmailStyle = VerificationEmailStyle.CODE,
                },
                SignInAliases = new SignInAliases {
                    Username = false,
                    Email = true,
                },
                AutoVerify = new AutoVerifiedAttrs {
                    Email = true,
                },
                KeepOriginal = new KeepOriginalAttrs {
                    Email = true,
                },
                Mfa = Mfa.OPTIONAL,
                MfaSecondFactor = new MfaSecondFactor {
                    Otp = true,
                },
                AccountRecovery = AccountRecovery.EMAIL_ONLY,
                StandardAttributes = new StandardAttributes {
                    Email = new StandardAttribute {
                        Required = true,
                        Mutable = true,
                    },
                    GivenName = new StandardAttribute {
                        Required = true,
                        Mutable = true,
                    },
                    FamilyName = new StandardAttribute {
                        Required = true,
                        Mutable = true,
                    },
                },
                PasswordPolicy = new PasswordPolicy {
                    MinLength = 8,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireDigits = true,
                    RequireSymbols = false,
                },
                RemovalPolicy = RemovalPolicy.DESTROY,
            });

            UserPoolDomain domain = userPool.AddDomain($"{appName}CognitoDomain", new UserPoolDomainOptions {
                CognitoDomain = new CognitoDomainOptions {
                    DomainPrefix = cognitoDomain
                },
                ManagedLoginVersion = ManagedLoginVersion.NEWER_MANAGED_LOGIN
            });

            UserPoolIdentityProviderGoogle googleProvider = new(this, $"{appName}IdentityProviderGoogle", new UserPoolIdentityProviderGoogleProps {
                UserPool = userPool,
                ClientId = googleClientId,
                ClientSecretValue = SecretValue.UnsafePlainText(googleClientSecret),
                Scopes = ["email", "profile"],
                AttributeMapping = new AttributeMapping() {
                    Email = ProviderAttribute.GOOGLE_EMAIL,
                    GivenName = ProviderAttribute.GOOGLE_GIVEN_NAME,
                    FamilyName = ProviderAttribute.GOOGLE_FAMILY_NAME,
                }
            });

            /*
            UserPoolIdentityProviderFacebook facebookProvider = new(this, $"{appName}IdentityProviderFacebook", new UserPoolIdentityProviderFacebookProps { 
                UserPool = userPool,
                ClientId = facebookClientId,
                ClientSecret = facebookClientSecret,
                Scopes = ["public_profile", "email"],
                AttributeMapping = new AttributeMapping() {
                    Email = ProviderAttribute.FACEBOOK_EMAIL,
                    GivenName = ProviderAttribute.FACEBOOK_FIRST_NAME,
                    FamilyName = ProviderAttribute.FACEBOOK_LAST_NAME,
                }
            });
            */

            /*
            UserPoolIdentityProviderOidc microsoftProvider = new(this, $"{appName}IdentityProviderMicrosoft", new UserPoolIdentityProviderOidcProps { 
                UserPool = userPool,
                ClientId = microsoftClientId,
                ClientSecret = microsoftClientSecret,
                IssuerUrl = "https://login.microsoftonline.com/common/v2.0",
                Scopes = [ "openid", "email", "profile" ],
                AttributeMapping = new AttributeMapping() {
                    Email = ProviderAttribute.OIDC_EMAIL,
                    GivenName = ProviderAttribute.OIDC_GIVEN_NAME,
                    FamilyName = ProviderAttribute.OIDC_FAMILY_NAME,
                }
            });
            */

            UserPoolClient userPoolClient = new(this, $"{appName}UserPoolClient", new UserPoolClientProps { 
                UserPoolClientName = $"{appName}UserPoolClient",
                UserPool = userPool,
                GenerateSecret = false,
                PreventUserExistenceErrors = true,
                AuthFlows = new AuthFlow {
                    UserSrp = true,
                },
                SupportedIdentityProviders = [
                    UserPoolClientIdentityProvider.COGNITO,
                    UserPoolClientIdentityProvider.GOOGLE,
                    // UserPoolClientIdentityProvider.FACEBOOK,
                    // UserPoolClientIdentityProvider.OIDC(microsoftProvider.UserPoolClientProviderName)
                ],
                OAuth = new OAuthSettings {
                    CallbackUrls = callbackUrls,
                    LogoutUrls = logoutUrls,
                    Flows = new OAuthFlows { AuthorizationCodeGrant = true },
                    Scopes = [ OAuthScope.OPENID, OAuthScope.EMAIL, OAuthScope.PROFILE ]
                }
            });
            userPoolClient.Node.AddDependency(googleProvider);

            _ = new CfnManagedLoginBranding(this, $"{appName}ManagedLoginBranding", new CfnManagedLoginBrandingProps {
                UserPoolId = userPool.UserPoolId,
                ClientId = userPoolClient.UserPoolClientId,
                ReturnMergedResources = true,
                Settings = new Dictionary<string, object> {
                    { "categories", new Dictionary<string, object> {
                        { "form", new Dictionary<string, object> {
                            { "languageSelector", new Dictionary<string, object> {
                                { "enabled", true }
                            }}
                        }},
                        { "global", new Dictionary<string, object> {
                            { "colorSchemeMode", "LIGHT" }
                        }}
                    }},
                    { "components", new Dictionary<string, object>{
                        { "pageBackground", new Dictionary<string, object> {
                            { "image", new Dictionary<string, object> {
                                { "enabled", false }
                            }},
                        }}
                    }}
                },
                Assets = (new List<CfnManagedLoginBranding.AssetTypeProperty>() {
                    new() {
                        Category = "FAVICON_SVG",
                        ColorMode = "DYNAMIC",
                        Extension = "SVG",
                        Bytes = "PHN2ZyBpZD0iQ2FwYV8xIiBlbmFibGUtYmFja2dyb3VuZD0ibmV3IDAgMCA1MTIgNTEyIiBoZWlnaHQ9IjUxMiIgdmlld0JveD0iMCAwIDUxMiA1MTIiIHdpZHRoPSI1MTIiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PHBhdGggZD0ibTQ4NC44NTQgMTE4LjIyLTE2LjQxLTQ1LjEwN2MtMS4zNTktMy43MzgtNC4xNDktNi43ODQtNy43NTUtOC40NjUtMy42MDUtMS42ODItNy43MjktMS44NjItMTEuNDctLjUwMmwtNDUuMTA3IDE2LjQxMWMtNy43ODQgMi44MzMtMTEuOCAxMS40MzktOC45NjcgMTkuMjI1IDIuODMxIDcuNzg1IDExLjQzNSAxMS43OTkgMTkuMjI1IDguOTY4bDEyLjY0NC00LjYtMTIwLjExMSAyMTYuODAzLTk0LjU4OC0zOS43OGMtNi4zMDEtMi42NTEtMTMuNTkzLS43MDYtMTcuNzM5IDQuNzI4bC0xMDEuNSAxMzNjLTUuMDI2IDYuNTg1LTMuNzYxIDE1Ljk5OCAyLjgyNCAyMS4wMjMgMi43MTggMi4wNzQgNS45MTYgMy4wNzcgOS4wODkgMy4wNzcgNC41MTcgMCA4Ljk4My0yLjAzMyAxMS45MzYtNS45MDFsOTQuNDU5LTEyMy43NzMgOTYuMzAxIDQwLjUwMWM3LjA2MyAyLjk2OSAxNS4yMjUuMTQxIDE4LjkzNy02LjU1OGwxMjYuNTQyLTIyOC40MTUgMy41IDkuNjIyYzIuMjE2IDYuMDkyIDcuOTY5IDkuODc2IDE0LjA5NyA5Ljg3NSAxLjcwMiAwIDMuNDM1LS4yOTIgNS4xMjctLjkwOCA3Ljc4NC0yLjgzMiAxMS44LTExLjQzOSA4Ljk2Ni0xOS4yMjR6IiBmaWxsPSIjYTVkMDJhIi8+PHBhdGggZD0ibTQ2NC43NTEgMjI2LjI5Yy0uMDU0IDAtLjEwNy0uMDAxLS4xNTktLjAwMS04LjIyNyAwLTE0LjkzOSA2LjYyNi0xNS4wMjUgMTQuODcybC0uMTA5IDEwLjQxNy0xMjQuNzYxLTEzNy44MjNjLTUuMzItNS44NzctMTQuMjg1LTYuNjExLTIwLjQ5LTEuNjgybC0xMDQuMzQ2IDgyLjg5Mi05OC4xMTIgMTkuMzMzYy04LjE0NCAxLjYwNS0xMy40NDQgOS41MDctMTEuODQgMTcuNjUxIDEuNjA1IDguMTQzIDkuNTAxIDEzLjQ0NCAxNy42NTEgMTEuODRsMTAxLjY5NS0yMC4wMzhjMi4zNTUtLjQ2NCA0LjU2My0xLjQ4NSA2LjQ0My0yLjk3N2w5Ni4xNzMtNzYuMzk5IDExNy4zNTUgMTI5LjY0MS0xMy4wNjYtLjEzN2MtLjA1NC0uMDAxLS4xMDctLjAwMS0uMTU5LS4wMDEtOC4yMjcgMC0xNC45MzkgNi42MjYtMTUuMDI1IDE0Ljg3Mi0uMDg3IDguMjk5IDYuNTcxIDE1LjA5OCAxNC44NzEgMTUuMTg0bDQ4LjA4OS41MDJjLjA1Mi4wMDEuMTA0LjAwMS4xNTcuMDAxIDMuOTI5IDAgNy43MDUtMS41MzkgMTAuNTE1LTQuMjkxIDIuODQ4LTIuNzg5IDQuNDcxLTYuNTk1IDQuNTEzLTEwLjU4MWwuNTAyLTQ4LjA4OWMuMDg2LTguMzAxLTYuNTcyLTE1LjA5OS0xNC44NzItMTUuMTg2eiIgZmlsbD0iI2U0NWE2ZSIvPjxwYXRoIGQ9Im00OTcgNDgyaC00NjJjLTIuNzU3IDAtNS0yLjI0My01LTV2LTk4aDE2YzguMjg0IDAgMTUtNi43MTYgMTUtMTVzLTYuNzE2LTE1LTE1LTE1aC0xNnYtMTA1aDE2YzguMjg0IDAgMTUtNi43MTYgMTUtMTVzLTYuNzE2LTE1LTE1LTE1aC0xNnYtMTA0LjYwNmgxNmM4LjI4NCAwIDE1LTYuNzE2IDE1LTE1cy02LjcxNi0xNS0xNS0xNWgtMTZ2LTY0LjM5NGMwLTguMjg0LTYuNzE2LTE1LTE1LTE1cy0xNSA2LjcxNi0xNSAxNXY0NjJjMCAxOS4yOTkgMTUuNzAxIDM1IDM1IDM1aDQ2MmM4LjI4NCAwIDE1LTYuNzE2IDE1LTE1cy02LjcxNi0xNS0xNS0xNXoiIGZpbGw9IiMzNTRhNjciLz48cGF0aCBkPSJtNDk3IDQ4MmgtMjA5LjExOXYzMGgyMDkuMTE5YzguMjg0IDAgMTUtNi43MTYgMTUtMTVzLTYuNzE2LTE1LTE1LTE1eiIgZmlsbD0iIzIzMzE0NSIvPjxwYXRoIGQ9Im00NjQuNzUxIDIyNi4yOWMtLjA1NCAwLS4xMDctLjAwMS0uMTU5LS4wMDEtOC4yMjcgMC0xNC45MzkgNi42MjYtMTUuMDI1IDE0Ljg3MmwtLjEwOSAxMC40MTctMTI0Ljc2MS0xMzcuODIzYy01LjMyLTUuODc3LTE0LjI4NS02LjYxMS0yMC40OS0xLjY4MmwtMTYuMzI3IDEyLjk3djM4LjM4OGwyMy45OS0xOS4wNTcgMTE3LjM1NSAxMjkuNjQxLTEzLjA2Ni0uMTM3Yy0uMDU0LS4wMDEtLjEwNy0uMDAxLS4xNTktLjAwMS04LjIyNyAwLTE0LjkzOSA2LjYyNi0xNS4wMjUgMTQuODcyLS4wODcgOC4yOTkgNi41NzEgMTUuMDk4IDE0Ljg3MSAxNS4xODRsNDguMDg5LjUwMmMuMDUyLjAwMS4xMDQuMDAxLjE1Ny4wMDEgMy45MjkgMCA3LjcwNS0xLjUzOSAxMC41MTUtNC4yOTEgMi44NDgtMi43ODkgNC40NzEtNi41OTUgNC41MTMtMTAuNTgxbC41MDItNDguMDg5Yy4wODctOC4zLTYuNTcxLTE1LjA5OC0xNC44NzEtMTUuMTg1eiIgZmlsbD0iI2Q4MmU0NCIvPjxwYXRoIGQ9Im00ODQuODU0IDExOC4yMi0xNi40MS00NS4xMDdjLTEuMzU5LTMuNzM4LTQuMTQ5LTYuNzg0LTcuNzU1LTguNDY1LTMuNjA1LTEuNjgyLTcuNzI5LTEuODYyLTExLjQ3LS41MDJsLTQ1LjEwNyAxNi40MTFjLTcuNzg0IDIuODMzLTExLjggMTEuNDM5LTguOTY3IDE5LjIyNSAyLjgzMSA3Ljc4NSAxMS40MzUgMTEuNzk5IDE5LjIyNSA4Ljk2OGwxMi42NDQtNC42LTEyMC4xMTEgMjE2LjgwMy0xOS4wMjItOHYzMi41NDVsMTkuODA0IDguMzI5YzcuMDYzIDIuOTY5IDE1LjIyNS4xNDEgMTguOTM3LTYuNTU4bDEyNi41NDItMjI4LjQxNSAzLjUgOS42MjJjMi4yMTYgNi4wOTIgNy45NjkgOS44NzYgMTQuMDk3IDkuODc1IDEuNzAyIDAgMy40MzUtLjI5MiA1LjEyNy0uOTA4IDcuNzg0LTIuODMxIDExLjgtMTEuNDM4IDguOTY2LTE5LjIyM3oiIGZpbGw9IiM5MGJjMDIiLz48L3N2Zz4="
                    },
                }).ToArray()
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoUserPoolId", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/UserPoolId",
                Description = $"Cognito UserPoolId de la aplicacion {appName}",
                StringValue = userPool.UserPoolId,
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoUserPoolClientId", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/UserPoolClientId",
                Description = $"User Pool Client ID de la aplicacion {appName}",
                StringValue = userPoolClient.UserPoolClientId,
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoRegion", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/Region",
                Description = $"Cognito Region de la aplicacion {appName}",
                StringValue = region,
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoCallbacks", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/Callbacks",
                Description = $"Cognito callbacks de la aplicacion {appName}",
                StringValue = String.Join(",", callbackUrls),
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoLogouts", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/Logouts",
                Description = $"Cognito logouts de la aplicacion {appName}",
                StringValue = String.Join(",", logoutUrls),
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoBaseUrl", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/BaseUrl",
                Description = $"Cognito base URL de la aplicacion {appName}",
                StringValue = domain.BaseUrl(),
                Tier = ParameterTier.STANDARD,
            });
        }
    }
}
