using System;
using System.Threading;
using System.Web.Http;
using PollyTestApp.Models;

namespace PollyTestApp.Controllers
{
    public class SpeedController : ApiController
    {
        [Route("api/speed/veryslow")]
        [HttpGet]
        public Message VerySlow()
        {
            Thread.Sleep(10000); // sleep for 10 seconds to make the call slow... zzz
            return new Message {Text = "VERY SLOW REQUEST RESULT"};
        }

        [Route("api/speed/various")]
        [HttpGet]
        public Message Various()
        {
            // generate a various sleep because we want a fuzzy various call
            int sleepTime = new Random().Next(1,5000);

            Thread.Sleep(sleepTime);
            return new Message { Text = $"Various Sleep Call - API slept for {sleepTime}ms." };
            }
    }
}