using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
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

            string cognitoCustomDomain = System.Environment.GetEnvironmentVariable("COGNITO_CUSTOM_DOMAIN") ?? throw new ArgumentNullException("COGNITO_CUSTOM_DOMAIN");
            string arnCognitoCertificate = System.Environment.GetEnvironmentVariable("ARN_COGNITO_CERTIFICATE") ?? throw new ArgumentNullException("ARN_COGNITO_CERTIFICATE");


            // Se obtienen los clients y secrets para los identity providers...
            // string microsoftClientId = System.Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_ID") ?? throw new ArgumentNullException("MICROSOFT_CLIENT_ID");
            // string microsoftClientSecret = System.Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_SECRET") ?? throw new ArgumentNullException("MICROSOFT_CLIENT_SECRET");

            string googleClientId = System.Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? throw new ArgumentNullException("GOOGLE_CLIENT_ID");
            string googleClientSecret = System.Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? throw new ArgumentNullException("GOOGLE_CLIENT_SECRET");

            string facebookClientId = System.Environment.GetEnvironmentVariable("FACEBOOK_CLIENT_ID") ?? throw new ArgumentNullException("FACEBOOK_CLIENT_ID");
            string facebookClientSecret = System.Environment.GetEnvironmentVariable("FACEBOOK_CLIENT_SECRET") ?? throw new ArgumentNullException("FACEBOOK_CLIENT_SECRET");

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
            });


            // Se busca certificado de cognito creado anteriormente...
            ICertificate certificate = Certificate.FromCertificateArn(this, $"{appName}CognitoCertificate", arnCognitoCertificate);

            UserPoolDomain domain = userPool.AddDomain($"{appName}CognitoDomain", new UserPoolDomainOptions {
                CustomDomain = new CustomDomainOptions {
                    DomainName = cognitoCustomDomain,
                    Certificate = certificate,
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
                    UserPoolClientIdentityProvider.FACEBOOK,
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
                    { "componentClasses", new Dictionary<string, object>{
                        { "focusState", new Dictionary<string, object>{
                            { "lightMode", new Dictionary<string, object> {
                                { "borderColor", "0069d9ff" }
                            }}
                        }},
                        { "input", new Dictionary<string, object>{
                            { "lightMode", new Dictionary<string, object> {
                                { "defaults", new Dictionary<string, object>{
                                    // { "borderColor", "0069d9ff" }
                                }},
                                { "placeholderColor", "6c757dff" },
                            }}
                        }},
                        { "inputLabel", new Dictionary<string, object>{
                            { "lightMode", new Dictionary<string, object> {
                                // { "textColor", "6c757dff" }
                            }}
                        }},
                        { "link", new Dictionary<string, object>{
                            { "lightMode", new Dictionary<string, object> {
                                { "defaults", new Dictionary<string, object>{
                                    { "textColor", "1b6ec2ff" }
                                }},
                                { "hover", new Dictionary<string, object>{
                                    { "textColor", "0069d9ff" }
                                }},
                            }}
                        }}
                    }},
                    { "components", new Dictionary<string, object>{
                        { "favicon", new Dictionary<string, object> {
                            { "enabledTypes", new string[1] { "ICO" }},
                        }},
                        { "form", new Dictionary<string, object> {
                            { "logo", new Dictionary<string, object> {
                                { "enabled", true }
                            }},
                        }},
                        { "pageBackground", new Dictionary<string, object> {
                            { "image", new Dictionary<string, object> {
                                { "enabled", false }
                            }},
                        }},
                        { "pageText", new Dictionary<string, object> {
                            { "lightMode", new Dictionary<string, object> {
                                { "headingColor", "212529ff" },
                                { "bodyColor", "212529ff" },
                                { "descriptionColor", "212529ff" },
                            }},
                        }},
                        { "primaryButton", new Dictionary<string, object> {
                            { "lightMode", new Dictionary<string, object> {
                                { "defaults", new Dictionary<string, object>{
                                    { "backgroundColor", "1b6ec2ff" },
                                    { "textColor", "ffffffff" }
                                }},
                                { "hover", new Dictionary<string, object>{
                                    { "backgroundColor", "0069d9ff" },
                                    { "textColor", "ffffffff" }
                                }},
                            }},
                        }},
                        { "secondaryButton", new Dictionary<string, object> {
                            { "lightMode", new Dictionary<string, object> {
                                { "defaults", new Dictionary<string, object>{
                                    { "backgroundColor", "ffffffff" },
                                    { "borderColor", "1b6ec2ff" },
                                    { "textColor", "1b6ec2ff" }
                                }},
                                { "hover", new Dictionary<string, object>{
                                    { "backgroundColor", "f2f8fdff" },
                                    { "borderColor", "0069d9ff" },
                                    { "textColor", "0069d9ff" }
                                }},
                            }},
                        }}
                    }}
                },
                Assets = (new List<CfnManagedLoginBranding.AssetTypeProperty>() {
                    new() {
                        Category = "FORM_LOGO",
                        ColorMode = "DYNAMIC",
                        Extension = "PNG",
                        // Bytes no está funcionando, es necesario subir las imagenes desde la consola de AWS...
                        Bytes = "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",
                    },
                    new() {
                        Category = "FAVICON_ICO",
                        ColorMode = "DYNAMIC",
                        Extension = "ICO",
                        // Bytes no está funcionando, es necesario subir las imagenes desde la consola de AWS...
                        Bytes = "AAABAAEAEBAAAAEAIABoBAAAFgAAACgAAAAQAAAAIAAAAAEAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA////Df///07///+r////8v////L///+r////Tv///w0AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD///8D////Sv//////////////////////////////////////////////Sv///wMAAAAAAAAAAAAAAAD///8DZkk1/2dJNf9nSTX/Z0k1/2dJNf9nSTX/X0Mw/0w1Jv9MNSb/TDUm/0w1Jv9OOCj/////AwAAAAAAAAAA////SmZJNP//////fte4/////////////////////////////////////////////////////0oAAAAA////Df////9mSTT//////y3OpP9W0a3/////////////////////////////////////////////////////Df///07/////Z0o1/42Bef/q9vL/KtCk/6Thyv//////wufY/1DIof///////////////////////////////07///+r/////2ZJNP///////////63gzf8q0KT/LM+j/yDLoP8Jv5T/ntbB/8G+7P+BeOH/9PP6//////////+r////8v////9mSTT/////////////////h9m9/4nZvf//////Lcaa/w7Alv9UQNz/RC7Y/2BQ3f//////////8v////L/////Z0k1/45/eP9uW+L/blvj/31u4//p6fb///////////8MvZf/QkzN/1I93P+CduH///////////L///+r/////2ZJNP//////8O/2/56U6P90YeT/bVrj/7+47P/08/r/QFLN/xalof////////////////////+r////Tv////9mSTT//////////////////////7Os6f9lT+H/Tjna/1VA3f8NwZX/KMWb/7Dcyv//////////Tv///w3/////Z0o1/5uPif//////////////////////yMXx/1dE2///////EsGX/wO8kP8qxJv//////////w0AAAAA////SmZKNP/o5ub//////////////////////////////////////3bOsP8Rw5b/8/b0/////0oAAAAAAAAAAP///wNmSTX//////////////////////////////////////////////////////////2P///8DAAAAAAAAAAAAAAAA////A////0r//////////////////////////////////////////////0r///8DAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA////Df///07///+r////8v////L///+r////Tv///w0AAAAAAAAAAAAAAAAAAAAA8A8AAMADAACAAQAAgAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAEAAIABAADAAwAA8A8AAA==",
                    }
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
