﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TD.KCN.WebApi.Application.Common.Exceptions;
using TD.KCN.WebApi.Application.Identity.Tokens;
using TD.KCN.WebApi.Infrastructure.Auth;
using TD.KCN.WebApi.Infrastructure.Auth.Jwt;
using TD.KCN.WebApi.Infrastructure.Multitenancy;
using TD.KCN.WebApi.Shared.Authorization;
using TD.KCN.WebApi.Shared.Multitenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.DirectoryServices.Protocols;
using TD.KCN.WebApi.Infrastructure.Ldap;

namespace TD.KCN.WebApi.Infrastructure.Identity;

internal class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer _t;
    private readonly SecuritySettings _securitySettings;
    private readonly JwtSettings _jwtSettings;
    private readonly TDTenantInfo? _currentTenant;
    private readonly LDAPSettings _ldapSettings;

    public TokenService(
        UserManager<ApplicationUser> userManager,
        IOptions<JwtSettings> jwtSettings,
        IStringLocalizer<TokenService> localizer,
        TDTenantInfo? currentTenant,
        IOptions<SecuritySettings> securitySettings,
        IOptions<LDAPSettings> ldapSettings)
    {
        _userManager = userManager;
        _t = localizer;
        _jwtSettings = jwtSettings.Value;
        _currentTenant = currentTenant;
        _securitySettings = securitySettings.Value;
        _ldapSettings = ldapSettings.Value;
    }

    public async Task<TokenResponse> GetTokenAsync(TokenRequest request, string ipAddress, CancellationToken cancellationToken)
    {
        ApplicationUser? user = null;
        if (!string.IsNullOrEmpty(request.Email))
        {
            user = await _userManager.FindByEmailAsync(request.Email.Trim().Normalize());
        }
        else if (!string.IsNullOrEmpty(request.UserName))
        {
            user = await _userManager.FindByNameAsync(request.UserName.Trim().Normalize());
        }

        if (string.IsNullOrWhiteSpace(_currentTenant?.Id) || user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedException(_t["Authentication Failed."]);
        }

        if (_securitySettings.RequireConfirmedAccount && !user.EmailConfirmed)
        {
            throw new UnauthorizedException(_t["E-Mail not confirmed."]);
        }

        if (_currentTenant.Id != MultitenancyConstants.Root.Id)
        {
            if (!_currentTenant.IsActive)
            {
                throw new UnauthorizedException(_t["Tenant is not Active. Please contact the Application Administrator."]);
            }

            if (DateTime.Now > _currentTenant.ValidUpto)
            {
                throw new UnauthorizedException(_t["Tenant Validity Has Expired. Please contact the Application Administrator."]);
            }
        }

        return await GenerateTokensAndUpdateUser(user, ipAddress);
    }

    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
    {
        var userPrincipal = GetPrincipalFromExpiredToken(request.Token);
        string? userEmail = userPrincipal.GetEmail();
        var user = await _userManager.FindByEmailAsync(userEmail!);
        if (user is null)
        {
            throw new UnauthorizedException(_t["Authentication Failed."]);
        }

        return await GenerateTokensAndUpdateUser(user, ipAddress);
    }

    private async Task<TokenResponse> GenerateTokensAndUpdateUser(ApplicationUser user, string ipAddress)
    {
        string token = GenerateJwt(user, ipAddress);


        await _userManager.UpdateAsync(user);

        return new TokenResponse(token);
    }

    private string GenerateJwt(ApplicationUser user, string ipAddress) =>
        GenerateEncryptedToken(GetSigningCredentials(), GetClaims(user, ipAddress));

    private IEnumerable<Claim> GetClaims(ApplicationUser user, string ipAddress) =>
        new List<Claim>
        {
            new(TDClaims.NameIdentifier, user.Id),
            new(TDClaims.Sub, user.UserName ?? string.Empty),
            new(TDClaims.Email, user.Email ?? string.Empty),
            new(TDClaims.IpAddress, ipAddress),
            new(TDClaims.Tenant, _currentTenant!.Id),
        };

    private static string GenerateRefreshToken()
    {
        byte[] randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private string GenerateEncryptedToken(SigningCredentials signingCredentials, IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityToken(
           claims: claims,
           expires: DateTime.Now.AddMinutes(_jwtSettings.TokenExpirationInMinutes),
           signingCredentials: signingCredentials);
        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key)),
            ValidateIssuer = false,
            ValidateAudience = false,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero,
            ValidateLifetime = false
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase))
        {
            throw new UnauthorizedException(_t["Invalid Token."]);
        }

        return principal;
    }

    private SigningCredentials GetSigningCredentials()
    {
        byte[] secret = Encoding.UTF8.GetBytes(_jwtSettings.Key);
        return new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);
    }

    #region Xử lý đăng nhập LDAP
    public async Task<TokenResponse> GetTokenLDDAPAsync(LoginLdapRequest request, string ipAddress, CancellationToken cancellationToken)
    {
        var adUser = new ADUser();
        var tmp = _ldapSettings;

        var searchResults = SearchInAD(
            _ldapSettings.LDAPserver,
            _ldapSettings.Port,
            _ldapSettings.Domain,
            request.UserName,
            request.Password,
            $"{_ldapSettings.LDAPQueryBase}",
            new StringBuilder("(|")
                .Append($"(sAMAccountName={request.UserName})")
                .Append($"(userPrincipalName={request.UserName})")
                .Append(")")
                .ToString(),
            SearchScope.Subtree,
            new string[]
            {
                    "objectGUID",
                    "sAMAccountName",
                    "displayName",
                    "mail",
                    "whenCreated",
                    "memberOf"
            });

        var results = searchResults.Entries.Cast<SearchResultEntry>();
        if (results.Any())
        {
            var resultsEntry = results.First();
            adUser = new ADUser()
            {
                objectGUID = new Guid((resultsEntry.Attributes["objectGUID"][0] as byte[])!),
                sAMAccountName = resultsEntry.Attributes["sAMAccountName"][0].ToString()!,
                /*mail = resultsEntry.Attributes["mail"][0].ToString()!,
                displayName = resultsEntry.Attributes["displayName"][0].ToString()!,
                whenCreated = DateTime.ParseExact(
                    resultsEntry.Attributes["whenCreated"][0].ToString()!,
                    "yyyyMMddHHmmss.0Z",
                    System.Globalization.CultureInfo.InvariantCulture
                )*/
            };

            var user = await _userManager.FindByNameAsync(adUser.sAMAccountName);

            if (user is null)
            {
                throw new UnauthorizedException(_t["Authentication Failed."]);
            }

            return await GenerateTokensAndUpdateUser(user, ipAddress);

        }
        else
        {
            throw new UnauthorizedException(_t["Authentication Failed."]);
        }

        throw new NotImplementedException();
    }

    public static SearchResponse SearchInAD(
        string ldapServer,
        int ldapPort,
        string domainForAD,
        string username,
        string password,
        string targetOU,
        string query,
        SearchScope scope,
        params string[] attributeList)
    {

        // https://github.com/dotnet/runtime/issues/63759#issuecomment-1019318988
        // on Windows the authentication type is Negotiate, so there is no need to prepend
        // AD user login with domain. On other platforms at the moment only
        // Basic authentication is supported
        var authType = AuthType.Basic;
        if (!OperatingSystem.IsWindows())
        {
            authType = AuthType.Basic;
            username = OperatingSystem.IsWindows()
                ? username
                : $"{domainForAD}\\{username}";
        }

        var connection = new LdapConnection(
            new LdapDirectoryIdentifier(ldapServer, ldapPort))
        {
            AuthType = authType,
            Credential = new(username, password)
        };
        connection.SessionOptions.ProtocolVersion = 3;

        connection.Bind();

        var request = new SearchRequest(targetOU, query, scope, attributeList);

        return (SearchResponse)connection.SendRequest(request);
    }
    #endregion
}