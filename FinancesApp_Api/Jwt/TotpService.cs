using Microsoft.Extensions.Logging;
using OtpNet;
using QRCoder;

namespace FinancesApp_Api.Jwt;

public class TotpService(ILogger<TotpService> logger)
{
    public TotpGenerationResult GenerateSecret(string userEmail)
    {
        byte[] secretKey = KeyGeneration.GenerateRandomKey(20);
        string base32Secret = Base32Encoding.ToString(secretKey);

        var otpAuthUri = $"otpauth://totp/FinancesApp:{userEmail}?secret={base32Secret}&issuer=FinancesApp&digits=6&period=30";

        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(5);
        var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);

        return new TotpGenerationResult(base32Secret, qrCodeBase64);
    }

    public bool VerifyCode(string base32Secret, string totpCode)
    {
        var secretBytes = Base32Encoding.ToBytes(base32Secret);
        var totp = new Totp(secretBytes, totpSize: 6);
        var expected = totp.ComputeTotp();
        var result = totp.VerifyTotp(totpCode, out var step, new VerificationWindow(previous: 4, future: 4));
        logger.LogInformation("[TotpService] Expected={Expected} | Got={Got} | StepMatched={Step} | ServerUtc={ServerUtc} | Result={Result}",
            expected, totpCode, step, DateTimeOffset.UtcNow, result);
        return result;
    }
}

public record TotpGenerationResult(string Base32Secret, string QrCodeBase64);
