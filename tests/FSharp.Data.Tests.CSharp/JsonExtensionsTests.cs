using System;
using System.Globalization;
using Microsoft.FSharp.Core;
using NUnit.Framework;

namespace FSharp.Data.Tests.CSharp
{
    [TestFixture]
    public class JsonExtensionTests
    {
        private const string TestJson = "{\"PropertyOne\": \"string value\"}";

        [Test]
        public void Properties_with_valid_JSON()
        {
            JsonValue jsonValue = JsonValue.Parse(TestJson, FSharpOption<CultureInfo>.Some(CultureInfo.CurrentCulture));
            
            var properties = jsonValue.Properties();
            
            Assert.AreEqual(1, properties.Length);
            Assert.AreEqual("PropertyOne", properties[0].Item1);
            Assert.AreEqual("string value", properties[0].Item2.AsString());
        }

        [Test]
        public void GetProperty_with_valid_JSON()
        {
            JsonValue jsonValue = JsonValue.Parse(TestJson, FSharpOption<CultureInfo>.Some(CultureInfo.CurrentCulture));
            var property = jsonValue.GetProperty("PropertyOne");
            Assert.AreEqual("string value", property.AsString());
        }

        [Test]
        public void TryGetProperty_with_valid_JSON()
        {
            JsonValue jsonValue = JsonValue.Parse(TestJson, FSharpOption<CultureInfo>.Some(CultureInfo.CurrentCulture));
            var property = jsonValue.TryGetProperty("PropertyTwo");
            Assert.AreEqual(FSharpOption<JsonValue>.None, property);
        }

        [Test]
        public void PropertyIndexer_with_valid_JSON()
        {
            var jsonValue = JsonValue.NewArray(new[] {JsonValue.NewNumber(1), JsonValue.NewNumber(2)});
            var property = jsonValue.Item(0);
            Assert.AreEqual(1, property.AsInteger());
        }

        [Test]
        public void AsInteger_with_valid_integer()
        {
            JsonValue jsonValue = JsonValue.NewString("123456");
            int result = jsonValue.AsInteger();
            Assert.AreEqual(123456, result);
        }

        [Test]
        public void AsInteger64_with_valid_integer()
        {
            JsonValue jsonValue = JsonValue.NewString("9223372036854775807");
            long result = jsonValue.AsInteger64();
            Assert.AreEqual(9223372036854775807, result);
        }

        [Test]
        public void AsDecimal_with_valid_decimal()
        {
            JsonValue jsonValue = JsonValue.NewString("123.456");
            decimal result = jsonValue.AsDecimal();
            Assert.AreEqual(123.456, result);
        }

        [Test]
        public void AsFloat_with_valid_float()
        {
            JsonValue jsonValue = JsonValue.NewString("0.1234567890");
            double result = jsonValue.AsFloat();
            Assert.AreEqual(0.1234567890d, result);
        }

        [Test]
        public void AsBoolean_with_valid_boolean()
        {
            JsonValue jsonValue = JsonValue.NewString("True");
            bool result = jsonValue.AsBoolean();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AsDateTime_with_valid_date()
        {
            JsonValue jsonValue = JsonValue.NewString("4/23/1982");
            DateTime result = jsonValue.AsDateTime();
            Assert.AreEqual(new DateTime(1982, 4, 23), result);
        }

        [Test]
        public void AsDateTime_with_valid_epoch_date_and_negative_timezone()
        {
            JsonValue jsonValue = JsonValue.NewString(@"/Date(1434067200000-0000)/");
            DateTime result = jsonValue.AsDateTime();
            Assert.AreEqual(new DateTime(2015, 6, 12), result);
        }

        [Test]
        public void AsDateTime_with_valid_epoch_date_and_positive_timezone()
        {
            JsonValue jsonValue = JsonValue.NewString(@"/Date(1434067200000+0000)/");
            DateTime result = jsonValue.AsDateTime();
            Assert.AreEqual(new DateTime(2015, 6, 12), result);
        }

        [Test]
        public void AsGuid_with_valid_guid()
        {
            const string guidStr = "4D36E965-E325-11CE-BFC1-08002BE10318";
            JsonValue jsonValue = JsonValue.NewString(guidStr);
            Guid result = jsonValue.AsGuid();
            Assert.AreEqual(new Guid(guidStr), result);
        }
    }
}