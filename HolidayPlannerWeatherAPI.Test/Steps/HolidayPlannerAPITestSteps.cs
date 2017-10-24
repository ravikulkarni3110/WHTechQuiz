using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using TechTalk.SpecFlow;
using System.Configuration;
using RestSharp;
using System.Collections.Generic;
using System.Net;
using System.Linq;

namespace HolidayPlannerWeatherAPI.Test.Steps
{
    [Binding]
    public class HolidayPlannerAPITestSteps
    {
        private UriBuilder baseURI { get; } = new UriBuilder("http://api.openweathermap.org/data/2.5/forecast");
        private DayOfWeek DayOfHoliday { get; set; }
        private IRestResponse Response { get; set; }
        private IList<double> MinimumTemp { get; } = new List<double>();
        private string City { get; set; }
        private JObject ResponseBody { get; set; }


        [Given(@"I like to holiday in (.*)")]
        public void GivenILikeToHolidayIn(string Location)
        {
            City = Location;
            string apikey = ConfigurationManager.AppSettings["apiKey"];

            baseURI.Query = "q=" + City + "&units=metric&appid=" + apikey;
            Console.WriteLine(baseURI.Uri);
        }

        [Given(@"I only like to holiday on (.*)")]
        public void GivenIOnlyLikeToHolidayOn(string dayToCheck)
        {
            DayOfWeek holidaydayOfWeek;
            if (Enum.TryParse(dayToCheck, true, out holidaydayOfWeek))
                DayOfHoliday = holidaydayOfWeek;
        }

        [When(@"I look up the weather forecast")]
        public void WhenILookUpTheWeatherForecast()
        {
            var restClient = new RestClient(baseURI.Uri);
            var restrequest = new RestRequest(Method.GET);
            Response = restClient.Execute(restrequest);

        }

        [Then(@"I receive the weather forecast")]
        public void ThenIReceiveTheWeatherForecast()
        {
            // Assert to validate status code recieved is a valid - 200
            Response.StatusCode.Should().Be(HttpStatusCode.OK, "need a valid response to know the forecast");

            //assert JSON response is valid
            IsValidJson(Response.Content).Should().BeTrue();

            //Ensure the response recieved is of requested City and Country
            ResponseBody = JObject.Parse(Response.Content);
            string[] cityAndCountry = City.Split(',');
            ResponseBody["city"]["name"].Value<string>()
                .Should()
                .Be(cityAndCountry[0], "returned forecast should of same city");
            //Check if country was passed in url if yes then validate the same in response.
            if (cityAndCountry.Length > 1)
            {
                ResponseBody["city"]["country"].Value<string>()
                    .Should()
                    .Be(cityAndCountry[1], "returned forecast should of same country");
            }

        }

        [Then(@"the temperature is warmer than (.*) degrees")]
        public void ThenTheTemperatureIsWarmerThanDegrees(string ExpectedMinimumTemperature)
        {
            double expectedMinumumTemp = Convert.ToDouble(ExpectedMinimumTemperature);
            var responseBody = JObject.Parse(Response.Content);
            var listOfForecasts = JArray.Parse(responseBody["list"].ToString());

            foreach (var forecast in listOfForecasts)
            {
                //Filter out forecast of particular day
                if (IsForecastFortheDayOfTheHoliday(forecast))
                {
                    //Retrieve minimum temparature of particular day
                    GetMinimumTemperature(forecast);
                }
            }

            MinimumTemp.Count.Should().BeGreaterThan(0, "We need the forecast for the day of the holiday");
            MinimumTemp.Min().Should().BeGreaterThan(expectedMinumumTemp, "It should be warmer");
        }


        private bool IsForecastFortheDayOfTheHoliday(JToken forecast)
        {

            var dateOfForcast = forecast["dt_txt"].Value<DateTime>();
            return dateOfForcast.DayOfWeek.Equals(DayOfHoliday);
        }

        private void GetMinimumTemperature(JToken forecast)
        {
            var minimumTempForQuarter = forecast["main"]["temp_min"].Value<double>();
            MinimumTemp.Add(minimumTempForQuarter);
        }

        public static bool IsValidJson(string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return false;
            }

            var value = stringValue.Trim();

            if ((value.StartsWith("{") && value.EndsWith("}")) || //For object
                (value.StartsWith("[") && value.EndsWith("]"))) //For array
            {
                try
                {
                    var obj = JToken.Parse(value);
                    return true;
                }
                catch (JsonReaderException)
                {
                    return false;
                }
            }

            return false;
        }
    }
}
