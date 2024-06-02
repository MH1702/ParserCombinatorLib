using ParserCombinatorLib;
using static ParserCombinatorLib.Parser;

namespace DSL {
	public static class DSLParser {
		private static Parser? function_parser = null;
		private static Parser? function_body_parser = null;
		private static Parser? function_arg_parser = null;
		private static Parser? function_args_parser = null;

		enum FunctionVisiblity {
			Private,
			Public,
		}

		static DSLParser() {
			function_arg_parser = Sequence([
				new() { ParserDirect = Many(Letter(), "fn_arg_type").Map(x => string.Concat(x)), Optional = false, Key = "fn_arg_type" },
				new() { ParserDirect = Whitespace(), Optional = false },
				new() { ParserDirect = Many(Letter(), "fn_arg_name").Map(x => string.Concat(x)), Optional = false, Key = "fn_arg_name" },
				new() { ParserDirect = Sequence([
					new() { ParserDirect = Whitespace(), Optional = false },
					new() { ParserDirect = Char('=', "fn_arg_default_marker"), Optional = false },
					new() { ParserDirect = Whitespace(), Optional = false },
					new() { ParserDirect = Many(Letter(), "fn_arg_default_value").Map(x => string.Concat(x)), Optional = false, Key = "fn_arg_default_value" },
				], "fn_arg"), Optional = false },
				new() { ParserDirect = Whitespace(), Optional = false },
			], "fn_arg");

			function_args_parser = Sequence([
				new() { ParserFunc = () => function_arg_parser, Optional = false, Key = "fn_args_first" },
				new() { ParserDirect = Many(Sequence([
					new() { ParserDirect = Char(',', ""), Optional = false },
					new() { ParserFunc = () => function_arg_parser, Optional = false, Key = "fn_args_rest" },
				], ""), ""), Optional = true, Key = "fn_args" },
			], "fn_args").Map(x => {
				Dumpify.DumpExtensions.Dump(x);

				return x;
			});

			function_parser = Sequence([
				new() { ParserDirect = Literal("pub", FunctionVisiblity.Public, "fn_vis_public"), Optional = true, Key = "fn_vis" },
				new() { ParserDirect = Whitespace(), Optional = false },
				new() { ParserDirect = Literal("fn", null, "fn_marker"), Optional = false },
				new() { ParserDirect = Whitespace(), Optional = false },
				new() { ParserDirect = Many(Letter(), "fn_name").Map(x => string.Concat(x)), Optional = false, Key = "fn_name" },

				new() { ParserDirect = Char('(', "fn_args_start"), Optional = false },
				new() { ParserDirect = Whitespace(), Optional = false },
				new() { ParserFunc = () => function_args_parser, Optional = true, Key = "fn_args" },
				new() { ParserDirect = Whitespace(), Optional = false },
				new() { ParserDirect = Char(')', "fn_args_end"), Optional = false },

				new() { ParserDirect = Whitespace(), Optional = false },

				new() { ParserDirect = Char('{', "fn_body_start"), Optional = false },
				new() { ParserDirect = Whitespace(), Optional = false },
				// TODO: fn_body_parser
				new() { ParserDirect = Whitespace(), Optional = true },
				new() { ParserDirect = Char('}', "fn_body_end"), Optional = false },
			], "fn").Map(x => {
				Dumpify.DumpExtensions.Dump(x);

				return x;
			});
		}

		public static dynamic? Parse(string input) {
			var result = function_parser.Parse(new ParserInput() { String = input.Trim(), Index = 0 });

			if (result.Success) return result.Value;

			else throw new ParsingException();
		}

		internal class Program {
			static void Main(string[] args) {
				DSLParser.Parse("pub fn main(int i = asd) {  }");
			}
		}
	}
}
