namespace EventsApp.Common
{
    public static class GlobalConstants
    {
        public static class Roles
        {
            public const string Admin = "Admin";
            public const string Organizer = "Organizer";
            public const string User = "User";
        }

        public static class User
        {
            public const int UserNameMinLength = 3;
            public const int UserNameMaxLength = 50;
            public const int BioMaxLength = 500;
            public const int ProfileImageUrlMaxLength = 500;
            public const int FirstNameMinLength = 1;
            public const int FirstNameMaxLength = 50;
            public const int LastNameMinLength = 1;
            public const int LastNameMaxLength = 50;
        }

        public static class Organizer
        {
            public const int OrganizationNameMinLength = 2;
            public const int OrganizationNameMaxLength = 100;
            public const int TaglineMaxLength = 140;
            public const int CityMaxLength = 80;
            public const int DescriptionMaxLength = 1000;
            public const int PhoneNumberMaxLength = 30;
            public const int WebsiteMaxLength = 200;
            public const int ContactEmailMaxLength = 120;
            public const int SocialUrlMaxLength = 200;
            public const int BrandColorMaxLength = 16;
            public const int CompanyNumberMaxLength = 50;
        }

        public static class Event
        {
            public const int TitleMinLength = 3;
            public const int TitleMaxLength = 150;
            public const int DescriptionMaxLength = 2000;
            public const int GenreMaxLength = 50;
            public const int ImageUrlMaxLength = 500;
            public const int AddressMaxLength = 200;
            public const int CityMaxLength = 80;
        }

        public static class Post
        {
            public const int ContentMinLength = 1;
            public const int ContentMaxLength = 3000;
            public const int ImageUrlMaxLength = 500;
        }

        public static class Social
        {
            public const int StoryMediaUrlMaxLength = 500;
            public const int StoryCaptionMaxLength = 280;
            public const int MessageContentMinLength = 1;
            public const int MessageContentMaxLength = 2000;
            public const int ActivityValueMaxLength = 128;
        }

        public static class Comment
        {
            public const int ContentMinLength = 1;
            public const int ContentMaxLength = 1000;
        }

        public static class Preferences
        {
            public const int PreferredGenreMaxLength = 50;
            public const int PreferredCityMaxLength = 80;
            public const int MinAgeLower = 0;
            public const int MinAgeUpper = 120;
        }

        public static class Ticket
        {
            public const int NameMinLength = 1;
            public const int NameMaxLength = 100;
            public const int DescriptionMaxLength = 1000;
            public const int ImageUrlMaxLength = 500;
            public const int QrCodeMaxLength = 200;
            public const int TransactionStatusMaxLength = 50;
        }

        public static class TransactionStatuses
        {
            public const string Pending = "Pending";
            public const string Paid = "Paid";
            public const string Failed = "Failed";
            public const string Cancelled = "Cancelled";
            public const string Refunded = "Refunded";
        }
    }
}
