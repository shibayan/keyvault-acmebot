namespace KeyVault.Acmebot.Options;

public class NamecheapOptions
{
    public int PropagationSeconds { get; set; } = 180;

    public string ApiKey { get; set; }

    public string ApiUser { get; set; }
}
