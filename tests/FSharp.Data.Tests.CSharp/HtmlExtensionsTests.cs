using System;
using FSharp.Data;
using Microsoft.FSharp.Collections;
using NUnit.Framework;

namespace FSharp.Data.Tests.CSharp
{
    [TestFixture]
    public class HtmlExtensionsTests
    {
        [Test]
        public void HtmlAttribute_Name_with_valid_attribute()
        {
            var attr = HtmlAttribute.New("id", "table_1");
            Assert.AreEqual("id", attr.Name());
        }

        [Test]
        public void HtmlAttribute_Value_with_valid_attribute()
        {
            var attr = HtmlAttribute.New("id", "table_1");
            Assert.AreEqual("table_1", attr.Value());
        }

        [Test]
        public void HtmlNode_Name_with_valid_element()
        {
            var node = CreateDivElement();
            Assert.AreEqual("div", node.Name());
        }

        [Test]
        public void HtmlNode_Child_no_children_with_text_element()
        {
            var node = HtmlNode.NewText("Hello");
            Assert.AreEqual(0, node.Elements().Length);
        }

        [Test]
        public void HtmlElement_TryGetAttribute_none_with_bad_attribute()
        {
            var node = CreateDivElement();
            Assert.AreEqual(null, node.TryGetAttribute("bad"));
        }

        [Test]
        public void HtmlElement_Elements_none_with_bad_attribute()
        {
            var node = CreateDivElement();
            Assert.IsTrue(node.Elements("bad").IsEmpty);
        }

        private static HtmlNode CreateDivElement()
        {
            var node = HtmlNode.NewElement(
                "div",
                new[] { Tuple.Create("id", "my_div"), Tuple.Create("class", "my_class") },
                new HtmlNode[0]);
            return node;
        }
    }
}
