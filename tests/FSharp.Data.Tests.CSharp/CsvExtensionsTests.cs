using System;
using NUnit.Framework;

namespace FSharp.Data.Tests.CSharp
{
    [TestFixture]
    public class CsvExtensionsTests
    {
        [Test]
        public void AsInteger_with_valid_integer()
        {
            const string intStr = "123456";
            int result = intStr.AsInteger();
            Assert.AreEqual(123456, result);
        }

        [Test]
        public void AsInteger64_with_valid_integer()
        {
            const string int64Str = "9223372036854775807";
            long result = int64Str.AsInteger64();
            Assert.AreEqual(9223372036854775807, result);
        }

        [Test]
        public void AsDecimal_with_valid_decimal()
        {
            const string decimalStr = "123.456";
            decimal result = decimalStr.AsDecimal();
            Assert.AreEqual(123.456, result);
        }

        [Test]
        public void AsFloat_with_valid_float()
        {
            const string floatStr = "0.1234567890";
            double result = floatStr.AsFloat();
            Assert.AreEqual(0.1234567890d, result);
        }

        [Test]
        public void AsBoolean_with_valid_boolean()
        {
            const string boolStr = "True";
            bool result = boolStr.AsBoolean();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AsDateTime_with_valid_date()
        {
            const string dateStr = "4/23/1982";
            DateTime result = dateStr.AsDateTime();
            Assert.AreEqual(new DateTime(1982, 4, 23), result);
        }

        [Test]
        public void AsGuid_with_valid_guid()
        {
            const string guidStr = "4D36E965-E325-11CE-BFC1-08002BE10318";
            Guid result = guidStr.AsGuid();
            Assert.AreEqual(new Guid(guidStr), result);
        }
    }
}