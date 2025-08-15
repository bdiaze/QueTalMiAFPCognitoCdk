using Amazon.CDK;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.SSM;
using Constructs;
using System;

namespace QueTalMiAfpCognitoCdk
{
    public class QueTalMiAfpCognitoCdkStack : Stack
    {
        internal QueTalMiAfpCognitoCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");
            string region = System.Environment.GetEnvironmentVariable("REGION_AWS") ?? throw new ArgumentNullException("REGION_AWS");

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
                    EmailVerified = ProviderAttribute.GOOGLE_EMAIL_VERIFIED,
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
                UserPool = userPool,
                GenerateSecret = false,
                PreventUserExistenceErrors = true,
                SupportedIdentityProviders = [
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
                UseCognitoProvidedValues = true
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoUserPoolId", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/UserPoolId",
                Description = $"Cognito UserPoolId de la aplicacion {appName}",
                StringValue = userPool.UserPoolId,
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoRegion", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/Region",
                Description = $"Cognito Region de la aplicacion {appName}",
                StringValue = region,
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoOAuth2TokenUrl", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/OAuth2TokenUrl",
                Description = $"URL de Cognito para negociar token de OAuth2 de la aplicacion {appName}",
                StringValue = domain.BaseUrl() + "/oauth2/token",
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterCognitoUserPoolClientId", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/UserPoolClientId",
                Description = $"User Pool Client ID de la aplicacion {appName}",
                StringValue = userPoolClient.UserPoolClientId,
                Tier = ParameterTier.STANDARD,
            });
        }
    }
}
