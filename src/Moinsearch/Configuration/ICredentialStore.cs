namespace Moinsearch.Configuration;

internal interface ICredentialStore
{
    string? Read(Uri wikiUrl);

    void Write(Uri wikiUrl, string password);
}
