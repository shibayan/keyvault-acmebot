namespace KeyVault.Acmebot.Options;

public class DomeneShopOptions
{
    public int PropagationSeconds { get; set; } = 180;
    public string ApiKeyUser { get; set; }
    public string ApiKeyPassword { get; set; }
}
