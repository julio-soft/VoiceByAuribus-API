# Cognito M2M Authentication - Technical Notes

## Problem
AWS Cognito tokens issued with `client_credentials` grant (M2M flow) **do not include the `aud` (audience) claim** by default. This causes JWT validation to fail with:
```
Bearer error="invalid_token", error_description="The audience 'empty' is invalid"
```

## Solution
The API now validates M2M tokens by:

1. **Disabling audience validation** (`ValidateAudience = false`)
2. **Validating scopes instead**: Tokens must contain at least one scope prefixed with the resource server identifier (`voice-by-auribus-api/`)
3. **OnTokenValidated event**: Custom validation ensures the `scope` claim contains valid resource server scopes

## Token Structure (M2M)
```json
{
  "sub": "1cgn2o0th0qh4av42jcbe4n1g6",
  "token_use": "access",
  "scope": "voice-by-auribus-api/base",
  "iss": "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_2GQIgX9Vw",
  "exp": 1699380941,
  "iat": 1699377341,
  "jti": "...",
  "client_id": "1cgn2o0th0qh4av42jcbe4n1g6"
}
```

**Note**: No `aud` claim is present. The resource server ID (`voice-by-auribus-api`) appears as a prefix in the `scope` claim.

## Available Scopes
- `voice-by-auribus-api/base` - Basic access to voice catalog and auth endpoints
- `voice-by-auribus-api/admin` - Admin access (exposes internal model paths)

## References
- [AWS Cognito Resource Servers](https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-pools-define-resource-servers.html)
- [OAuth 2.0 Client Credentials Grant](https://oauth.net/2/grant-types/client-credentials/)
