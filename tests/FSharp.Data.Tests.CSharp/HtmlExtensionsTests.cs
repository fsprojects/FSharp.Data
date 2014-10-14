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
            var attr = HtmlAttribute.NewHtmlAttribute("id", "table_1");
            Assert.AreEqual("id", attr.Name());
        }

        [Test]
        public void HtmlAttribute_Value_with_valid_attribute()
        {
            var attr = HtmlAttribute.NewHtmlAttribute("id", "table_1");
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
            var node = HtmlNode.NewHtmlText("Hello");
            Assert.AreEqual(0, node.Children().Length);
        }

        [Test]
        public void HtmlElement_TryGetAttribute_none_with_bad_attribute()
        {
            var node = CreateDivElement();
            Assert.AreEqual(Microsoft.FSharp.Core.FSharpOption<string>.None, node.TryGetAttribute("bad"));
        }

        private static HtmlNode CreateDivElement()
        {
            var node = HtmlNode.NewHtmlElement(
                "div",
                ListModule.OfSeq(new[] { HtmlAttribute.NewHtmlAttribute("id", "my_div"), HtmlAttribute.NewHtmlAttribute("class", "my_class") }),
                FSharpList<HtmlNode>.Empty);
            return node;
        }
    }
}
