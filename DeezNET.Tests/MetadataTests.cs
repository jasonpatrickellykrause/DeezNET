using Newtonsoft.Json.Linq;
using Shouldly;

namespace DeezNET.Tests
{
    public class MetadataTests
    {
        [Theory]
        [InlineData("2021-06-04", 2021u)]
        [InlineData("1999", 1999u)]
        public void ParseYearReadsYear(string date, uint year)
        {
            Downloader.ParseYear(new JValue(date)).ShouldBe(year);
        }

        [Theory]
        [InlineData("0000-00-00")]
        [InlineData("")]
        [InlineData("n/a")]
        [InlineData("-123-01-01")]
        public void ParseYearTreatsInvalidDatesAsMissing(string date)
        {
            Downloader.ParseYear(new JValue(date)).ShouldBeNull();
        }

        [Fact]
        public void ParseYearTreatsMissingDateAsMissing()
        {
            Downloader.ParseYear(null).ShouldBeNull();
        }
    }
}
