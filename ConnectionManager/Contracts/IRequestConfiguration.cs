using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager.Contracts
{
    public interface IRequestConfiguration
    {
        void AddHeader(string name, string value);
        void SetReferer(string url);
        void SetUserAgent(string userAgent);
    }
}
