namespace Cargo.BuildingBlocks.Security.Keycloak;

public class KeycloakSettings
{
    public required string BaseUrl { get; set; }
    public required string Realm { get; set; }
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
}