namespace PFWolf.Common.Assets
{
    [Obsolete("This will eventually be split up into PCSound, AdLibSound, DigiSound, ImfMusic")]
    public record Audio : Asset
    {
        public Audio()
        {
            Type = AssetType.DigitizedSound;
        }
    }
}
