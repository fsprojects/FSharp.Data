namespace FSharp.Data.Authentication

open System
open System.Net
open System.Security
open System.Text

[<AutoOpen>]
module AuthenticationUtils =
    
    [<Literal>]
    let BasicAuthType = "Basic"

    [<Literal>]
    let DigestAuthType = "Digest"   

    let(|Url|_|) str =
        match Uri.TryCreate(str, UriKind.Absolute) with
        | (true, url) when url.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) -> Some(url)
        | _ -> None
      
    let isValidUrl str = 
        match str with
        | Url _ -> true
        | _ -> false

    let hasAuthorizationPart url =
        if isValidUrl url then
            match(new Uri(url)).UserInfo.Split([|':'|]) with
            | [|_; _|] -> true
            | _ -> false
        else
            false

    let createBasicAuthTicket(domain, userName, password, encoding:Encoding) =
        let ticket = if not (String.IsNullOrEmpty(domain)) then domain + "\\" else "" + userName + ":" + password
        BasicAuthType + " " + Convert.ToBase64String(encoding.GetBytes(ticket))
        
    let isNull value = obj.ReferenceEquals(value, null)

    // Inspired by https://stackoverflow.com/questions/6817852/handling-null-values-in-f .
    let(|NotNull|_|) value = 
        if isNull value then None else Some()

#if FX_NO_WEBREQUEST_AUTH
#else
    let toSecureString str =
        let securedStr = new SecureString()
        String.iter securedStr.AppendChar str
        securedStr

    let createBasicAuthTicketWithCredentials(credentials:NetworkCredential, encoding:Encoding) =
            // Note that NetworkCredential represents non-ASCII characters as percentange escaped unicode strings (a sequence of chars). Example: "passwdä" -> "passwd%C3%A4".
            // Base64 encoding a string containing escape characters would result to different autcome than encoding the plain unicode string.
            // TODO: The credentials should be handled as SecureString by pinning the pointer and manipulating the raw bytes...
            createBasicAuthTicket(credentials.Domain, credentials.UserName, System.Uri.UnescapeDataString(credentials.Password), encoding)
    
    let removeAuthorizationPart(uri:Uri) =
        new Uri(sprintf "%s%s%s%s" uri.Scheme Uri.SchemeDelimiter uri.Authority uri.PathAndQuery)

    let removeQueryPart(uri:Uri) =
        new Uri(sprintf "%s%s%s%s" uri.Scheme Uri.SchemeDelimiter uri.Authority uri.AbsolutePath)

#endif 

module Authentication =
    begin 

#if FX_NO_WEBREQUEST_AUTH
#else                
        let shouldSendCredentials(policy:ICredentialPolicy, request:WebRequest, credential:NetworkCredential, authenticationModule) =
            isNull policy || policy.ShouldSendCredential(request.RequestUri, request, credential, authenticationModule)
                          
        let isBasicAuthChallenge(challenge: string) =
            challenge.StartsWith(BasicAuthType, StringComparison.OrdinalIgnoreCase)
  
        // This was inspired by Yishai Galatzer's post at http://blogs.iis.net/yigalatz/archive/2010/11/24/replacing-the-built-in-basic-authentication-module-to-support-non-english-characters-in-a-httpwebrequest.aspx .
        type BasicAuthentication() =

            member private this.buildAuthorization(policy:ICredentialPolicy, request:WebRequest, credentials:NetworkCredential) =
                if shouldSendCredentials(policy, request, credentials, this) then new Authorization(createBasicAuthTicketWithCredentials(credentials, Encoding.UTF8), true) else null
    
            interface IAuthenticationModule with
                member this.AuthenticationType with get() = BasicAuthType
                member this.CanPreAuthenticate with get() = true
         
                member this.Authenticate(challenge, request, credentials) =
                    match challenge, request, credentials with
                    | NotNull, NotNull, NotNull ->
                        if not (isBasicAuthChallenge challenge) then null else
                            let nc = credentials.GetCredential(request.RequestUri, BasicAuthType)
                            this.buildAuthorization(AuthenticationManager.CredentialPolicy, request, nc)
                    | _ -> null
           
                member this.PreAuthenticate(request, credentials) =
                    match request, credentials with
                    | NotNull, NotNull ->
                        let nc = credentials.GetCredential(request.RequestUri, BasicAuthType)
                        match nc with
                        | NotNull ->
                            let nc = credentials.GetCredential(request.RequestUri, BasicAuthType)
                            this.buildAuthorization(AuthenticationManager.CredentialPolicy, request, nc)
                        | _ -> null
                    | _ -> null
#endif
    end

[<AutoOpen>]
module AuthenticationRegistration =
    let registerAllAuthenticationModules() =
#if FX_NO_WEBREQUEST_AUTH
#else 
        AuthenticationManager.Unregister(AuthenticationUtils.BasicAuthType)
        AuthenticationManager.Register(Authentication.BasicAuthentication())

        // Register here possible OAuth2 module as well...
#endif
        ()