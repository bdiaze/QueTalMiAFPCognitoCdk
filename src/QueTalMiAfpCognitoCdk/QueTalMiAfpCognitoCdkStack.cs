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
                    { "componentClasses", new Dictionary<string, object>{
                        { "focusState", new Dictionary<string, object>{
                            { "lightMode", new Dictionary<string, object> {
                                { "borderColor", "0069d9ff" }
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
                        { "pageBackground", new Dictionary<string, object> {
                            { "image", new Dictionary<string, object> {
                                { "enabled", false }
                            }},
                        }},
                        { "pageText", new Dictionary<string, object> {
                            { "lightMode", new Dictionary<string, object> {
                                { "headingColor", "212529ff" },
                                { "bodyColor", "6c757dff" },
                                { "descriptionColor", "6c757dff" },
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
                        }}
                    }}
                },
                Assets = (new List<CfnManagedLoginBranding.AssetTypeProperty>() {
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
