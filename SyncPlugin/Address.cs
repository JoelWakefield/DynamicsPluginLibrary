using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SyncPlugin
{
    [DataContract]
    public class Address
    {
        [DataMember(Name = "street1")]
        public string Street1 { get; set; }
        [DataMember(Name = "street2")]
        public string Street2 { get; set; }
        [DataMember(Name = "city")]
        public string City { get; set; }
        [DataMember(Name = "state")]
        public string State { get; set; }
        [DataMember(Name = "zip5")]
        public string Zip5 { get; set; }
        [DataMember(Name = "zip4")]
        public string Zip4 { get; set; }

        public override string ToString()
        {
            return $"{Street1} {Street2} {City} {State} {Zip5}{(Zip4 == null ? "" : $"-{Zip4}")}";
        }
    }

    public class AddressValidateResponse
    {
        public Address Address { get; set; }
    }
}
