using System.Globalization;

namespace ApexPost.Models.ValueObjects;

/// <summary>Parcel dimensions in centimetres, with the volumetric weight couriers charge on.</summary>
public readonly record struct Dimensions
{
    /// <summary>Industry-standard volumetric divisor (cm³ per kg).</summary>
    public const decimal VolumetricDivisor = 5000m;

    public Dimensions(decimal lengthCm, decimal widthCm, decimal heightCm)
    {
        if (lengthCm <= 0m || widthCm <= 0m || heightCm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthCm), "All dimensions must be greater than zero.");
        }

        LengthCm = lengthCm;
        WidthCm = widthCm;
        HeightCm = heightCm;
    }

    public decimal LengthCm { get; }
    public decimal WidthCm { get; }
    public decimal HeightCm { get; }

    public decimal VolumeCm3 => LengthCm * WidthCm * HeightCm;

    public Weight VolumetricWeight => new(Math.Round(VolumeCm3 / VolumetricDivisor, 3));

    public decimal LongestSideCm => Math.Max(LengthCm, Math.Max(WidthCm, HeightCm));

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{LengthCm:0.#} x {WidthCm:0.#} x {HeightCm:0.#} cm");
}
