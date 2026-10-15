using NUnit.Framework;
using System;
using WinMemoryCleaner;

namespace WinMemoryCleaner.Test
{
    /// <summary>
    /// Testes de Helper.FormatJson (lógica pura, sem dependência de UI/admin).
    /// </summary>
    [TestFixture]
    public class FormatJsonTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void FormatJson_NullOrEmpty_ReturnsEmpty(string json)
        {
            Assert.That(Helper.FormatJson(json), Is.EqualTo(string.Empty));
        }

        [Test]
        public void FormatJson_SimpleObject_IndentsWithTwoSpaces()
        {
            var nl = Environment.NewLine;

            var result = Helper.FormatJson("{\"a\":1,\"b\":2}");

            Assert.That(result, Is.EqualTo("{" + nl + "  \"a\": 1," + nl + "  \"b\": 2" + nl + "}"));
        }

        [Test]
        public void FormatJson_StringContent_IsPreserved()
        {
            // espaços, vírgulas, chaves e aspas escapadas dentro de strings não podem ser alterados
            var result = Helper.FormatJson("{\"a\":\"x, y {z} \\\"q\\\"\"}");

            Assert.That(result, Does.Contain("\"x, y {z} \\\"q\\\"\""));
        }

        [Test]
        public void FormatJson_WhitespaceOutsideStrings_IsRemoved()
        {
            var compact = Helper.FormatJson("{\"a\":[1,2]}");
            var spaced = Helper.FormatJson("{ \"a\" :\t[ 1 ,\r\n 2 ] }");

            Assert.That(spaced, Is.EqualTo(compact));
        }
    }
}
