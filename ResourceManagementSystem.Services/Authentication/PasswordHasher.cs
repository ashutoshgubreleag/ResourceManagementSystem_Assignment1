using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ResourceManagementSystem.Services.Authentication;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}

/// <summary>
/// PBKDF2-SHA256 with a per-user random salt, stored as <c>PBKDF2$iterations$salt$hash</c>.
/// Verification uses <see cref="CryptographicOperations.FixedTimeEquals"/> so response time
/// does not leak how many bytes matched.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // Used when the email is unknown, so "no such user" costs as much time as "wrong password".
    private static readonly string DummyHash = new Pbkdf2PasswordHasher().Hash("not-a-real-password");

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return string.Create(CultureInfo.InvariantCulture, $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public bool Verify(string password, string encodedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        var parts = (string.IsNullOrEmpty(encodedHash) ? DummyHash : encodedHash).Split('$');
        if (parts is not ["PBKDF2", var iterationText, var saltText, var hashText]
            || !int.TryParse(iterationText, NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        var expected = Convert.FromBase64String(hashText);
        var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(saltText), iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected) && !string.IsNullOrEmpty(encodedHash);
    }
}
