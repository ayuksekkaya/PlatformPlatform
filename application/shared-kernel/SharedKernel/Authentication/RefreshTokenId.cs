using PlatformPlatform.SharedKernel.IdGenerators;

namespace PlatformPlatform.SharedKernel.Authentication;

[PublicAPI]
[IdPrefix("rt")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, RefreshTokenId>))]
public sealed record RefreshTokenId(string Value) : StronglyTypedUlid<RefreshTokenId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}
