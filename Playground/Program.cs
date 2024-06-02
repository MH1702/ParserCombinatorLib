namespace Playground {
	internal class Program {
		static void Main(string[] args) {
			var input = @"{ ""arr"": [123,""B \u1234 \r"",true, { ""a"": ""b"", ""inner"": { ""c"": ""d"" } }], ""obj"": { ""a"": ""b"", ""inner"": { ""c"": ""d"" } } }";

			var result = ParserCombinatorLib.JsonParser.Parse(input);

			Dumpify.DumpExtensions.Dump(result);
		}
	}
}
