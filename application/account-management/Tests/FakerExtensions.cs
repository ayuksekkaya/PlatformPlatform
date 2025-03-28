using Bogus;
using PlatformPlatform.SharedKernel.StronglyTypedIds;

namespace PlatformPlatform.AccountManagement.Tests;

public static class FakerExtensions
{
    public static string TenantName(this Faker faker)
    {
        return new string(faker.Company.CompanyName().Take(30).ToArray());
    }

    public static string PhoneNumber(this Faker faker)
    {
        var random = new Random();
        return $"+{random.Next(1, 9)}-{faker.Phone.PhoneNumberFormat()}";
    }

    public static string InvalidEmail(this Faker faker)
    {
        return faker.Internet.ExampleEmail(faker.Random.AlphaNumeric(100));
    }

    public static long RandomId(this Faker faker)
    {
        return IdGenerator.NewId();
    }
}
