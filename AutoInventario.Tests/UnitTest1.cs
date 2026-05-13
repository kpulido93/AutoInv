namespace AutoInventario.Tests;

public class UnitTest1
{
    [Fact]
    public void Agent_project_embeds_public_key()
    {
        string publicKey = AutoInventario.Services.CryptoService.LoadPublicKey();

        Assert.Contains("BEGIN PUBLIC KEY", publicKey);
    }
}
