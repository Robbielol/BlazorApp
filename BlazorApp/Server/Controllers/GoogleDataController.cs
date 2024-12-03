using BlazorApp.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BlazorApp.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GoogleDataController : ControllerBase
    {
        private readonly ILogger<GoogleDataController> _logger;

        public GoogleDataController(ILogger<GoogleDataController> logger)
        {
            _logger = logger;
        }
            
        [HttpGet]
        public ActionResult<List<Company>> GetBusinessesPlaceIDAsync()
        {
            string query = "Hair Salons on Commercial Drive";
            double radius = 10000;
            string apiKey = "AIzaSyBQcVCnvdo13C5OMRaonkLKpPw1sPuN9Ds";
            string location = "49.2827,-123.1207";
            string baseUrl = "https://maps.googleapis.com/maps/api/place/textsearch/json";
            HttpClient client = new HttpClient();
            var allResults = new List<JObject>();
            string nextPageToken = null;

            do
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "key", apiKey },
                { "location", location },
                { "radius", radius.ToString() },
                { "query", query }
            };

                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    parameters.Add("pagetoken", nextPageToken);
                }

                string url = $"{baseUrl}?{string.Join("&", parameters.Select(x => $"{x.Key}={x.Value}"))}";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
               
                var encoding = ASCIIEncoding.ASCII;
                JObject data;
                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), encoding))
                {
                    string responseText = reader.ReadToEnd();
                    data = JObject.Parse(responseText);
                };
                allResults.AddRange(data["results"]?.ToObject<List<JObject>>() ?? new List<JObject>());
                nextPageToken = data["next_page_token"]?.ToString();

                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    Task.Delay(2000); // Delay for the next page token to activate
                }
            } while (!string.IsNullOrEmpty(nextPageToken));

            return GetBusinessesWithoutWebsite(allResults, apiKey);
        }

        public ActionResult<List<Company>> GetBusinessesWithoutWebsite(List<JObject> placeIdList, string apiKey)
        {
            HttpClient client = new HttpClient();
            List<Company> businessNoWebsiteList = new List<Company>();
            foreach (var place in placeIdList)
            {
                string placeId = place["place_id"]?.ToString();
                if (string.IsNullOrEmpty(placeId)) continue;

                string detailsUrl = "https://maps.googleapis.com/maps/api/place/details/json";
                var parameters = new Dictionary<string, string>
                {
                    { "key", apiKey },
                    { "place_id", placeId }
                };

                string url = $"{detailsUrl}?{string.Join("&", parameters.Select(x => $"{x.Key}={x.Value}"))}";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                var encoding = ASCIIEncoding.ASCII;
                JObject placeData;
                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), encoding))
                {
                    string responseText = reader.ReadToEnd();
                    placeData = JObject.Parse(responseText);
                };

                if (placeData["result"] != null)
                {
                    var placeDetails = placeData["result"];
                    string website = placeDetails["website"]?.ToString() ?? "No name available";
                    double rating = placeDetails["rating"]?.ToObject<double>() ?? 0;

                    if ((string.IsNullOrEmpty(website) || website.Contains("facebook") || website.Contains("instagram")) && rating >= 3.9)
                    {
                        string phoneNum = placeDetails["international_phone_number"]?.ToString() ?? "No phone number available";
                        string address = placeDetails["formatted_address"]?.ToString() ?? "No address available";

                        businessNoWebsiteList.Add(new Company()
                        {
                            Name = placeDetails["name"]?.ToString(),
                            Website = website,
                            Rating = rating,
                            PhoneNumber = phoneNum,
                            Address = address
                        });
                    }
                }
                else
                {
                    Console.WriteLine("Error: No place details found.");
                }
            }
            Console.WriteLine(businessNoWebsiteList);

            return businessNoWebsiteList;
        }
    }
}
