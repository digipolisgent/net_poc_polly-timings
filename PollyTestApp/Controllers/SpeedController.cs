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
    }
}