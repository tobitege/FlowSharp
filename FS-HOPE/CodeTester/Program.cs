﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using Newtonsoft.Json;

using Clifton.Core.Semantics;

namespace CodeTester
{
    public class ST_SerializeToXml : ISemanticType
    {
        public object Object { get; set; }
        public Func<string, ISemanticType> Continuation { get; set; }
    }

    public class ST_XmlHttpGet : ISemanticType
    {
        public string Url { get; set; }
        public Func<string, ISemanticType> Continuation { get; set; }
    }

    public class ST_DeserializeFromXml : ISemanticType
    {
        public string Xml { get; set; }
        public Type Instance { get; set; }
    }

    public class ST_USPSAddressResponse : ISemanticType
    {
        public int ID { get; set; }
        public string FirmName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Urbanization { get; set; }
        public string Zip5 { get; set; }
        public string Zip4 { get; set; }
        public string DeliveryPoint { get; set; }
        public string CarrierRoute { get; set; }
        public string DPVConfirmation { get; set; }
        public string DPVCMRA { get; set; }
        public string DPVFootnotes { get; set; }
        public string Business { get; set; }
        public string CentralDeliveryPoint { get; set; }
        public string Vacant { get; set; }
    }

    public class AddressValidateResponse
    {
        public ST_USPSAddressResponse Address { get; set; }
    }

    public class Address
    {
        [XmlAttribute] public int ID { get; set; }
        public string FirmName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Urbanization { get; set; }
        public string Zip5 { get; set; }
        public string Zip4 { get; set; }

        public Address()
        {
            ID = 0;         // Only 1 address
            // These elements must be serialized even if not populated.
            Address1 = "";
            Zip4 = "";
        }
    }

    public class AddressValidateRequest
    {
        [XmlAttribute] public string USERID { get; set; }
        public int Revision { get; set; }
        public Address Address { get; set; }

        public AddressValidateRequest()
        {
            USERID = "457INTER2602";
            Revision = 1;
            Address = new Address();
        }
    }

    public class Foo
    {
        [Category("A")]
        [Description("A Text")]
        public string Text { get; set; }

        [Category("A")]
        public DateTime Date { get; set; }
        [Category("A")]
        public Bar Bar { get; set; }
    }

    public class Bar
    {
        [Category("B")]
        public int I { get; set; }

        public int J { get; set; }
    }

    public class ST_Address : ISemanticType
    {
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public ST_City City { get; set; }
        public ST_State State { get; set; }
        public ST_Zip Zip { get; set; }

        public ST_Address()
        {
            City = new ST_City();
            State = new ST_State();
            Zip = new ST_Zip();
        }
    }

    public class ST_Zip : ISemanticType
    {
        public ST_Zip5 Zip5 { get; set; }
        public ST_Zip4 Zip4 { get; set; }

        public ST_Zip()
        {
            Zip5 = new ST_Zip5();
            Zip4 = new ST_Zip4();
        }
    }

    public class ST_Zip4 : ISemanticType
    {
        public string Zip4 { get; set; }
    }

    public class ST_Zip5 : ISemanticType
    {
        public string Zip5 { get; set; }
    }

    public class ST_City : ISemanticType
    {
        public string City { get; set; }
    }

    public class ST_State : ISemanticType
    {
        public string State { get; set; }
    }

    public class PropertyContainer
    {
        public List<PropertyData> Types { get; set; }

        public PropertyContainer()
        {
            Types = new List<PropertyData>();
        }
    }

    public class PropertyData
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public PropertyContainer ChildType { get; set; }

        public PropertyData()
        {
        }
    }

    class Program
    {
        public static string Get(string uri)
        {
            var client = new WebClient();

            // Add a user agent header in case the
            // requested URI contains a query.

            client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

            var data = client.OpenRead(uri);
            var reader = new StreamReader(data);
            var s = reader.ReadToEnd();
            data.Close();
            reader.Close();

            return s;
        }

        static void Main(string[] args)
        {
            var avr = new AddressValidateRequest
            {
                Address =
                {
                    Address2 = "565 Roxbury Rd",
                    City = "Hudson",
                    State = "NY",
                    Zip5 = "12534"
                }
            };

            // All this necessary to omit the XML declaration and remove namespaces.  Sigh.
            var xws = new XmlWriterSettings
            {
                OmitXmlDeclaration = true
            };
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            var xs = new XmlSerializer(avr.GetType());
            var sb = new StringBuilder();
            var tw = new StringWriter(sb);
            var xtw = XmlWriter.Create(tw, xws);
            xs.Serialize(xtw, avr, ns);
            var xml = sb.ToString();
            var ret = Get("https://secure.shippingapis.com/ShippingAPI.dll?API=Verify&XML=" + xml);
            // var ret = "<?xml version =\"1.0\" encoding=\"UTF-8\"?><AddressValidateResponse><Address ID=\"0\"><Address2>565 ROXBURY RD</Address2><City>HUDSON</City><State>NY</State><Zip5>12534</Zip5><Zip4>3626</Zip4><DeliveryPoint>65</DeliveryPoint><CarrierRoute>R001</CarrierRoute><DPVConfirmation>Y</DPVConfirmation><DPVCMRA>N</DPVCMRA><DPVFootnotes>AABB</DPVFootnotes><Business>N</Business><CentralDeliveryPoint>N</CentralDeliveryPoint><Vacant>N</Vacant></Address></AddressValidateResponse>";

            var xs2 = new XmlSerializer(typeof(AddressValidateResponse));
            var sr = new StringReader(ret);
            var resp = (AddressValidateResponse)xs2.Deserialize(sr);

            var t = typeof(ST_Address);
            var pis = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var pc = new PropertyContainer();
            BuildTypes(pc, pis);
            var json = JsonConvert.SerializeObject(pc);
        }

        static void BuildTypes(PropertyContainer pc, PropertyInfo[] pis)
        {
            foreach (var pi in pis)
            {
                var pd = new PropertyData
                {
                    Name = pi.Name, TypeName = pi.PropertyType.FullName,
                    Category = pi.GetCustomAttribute<CategoryAttribute>()?.Category,
                    Description = pi.GetCustomAttribute<DescriptionAttribute>()?.Description
                };
                pc.Types.Add(pd);

                if ((!pi.PropertyType.IsValueType) && (pd.TypeName != "System.String"))
                {
                    var pisChild = pi.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    pd.ChildType = new PropertyContainer();
                    BuildTypes(pd.ChildType, pisChild);
                }
            }
        }
    }
}
