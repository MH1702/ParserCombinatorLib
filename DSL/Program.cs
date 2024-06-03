using ParserCombinatorLib;
using System.Diagnostics;
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

		readonly struct FunctionArg {
			public required string Type { get; init; }
			public required string Name { get; init; }
			public required bool HasDefaultValue { get; init; }
			public string? DefaultValue { get; init; }
		}

		static DSLParser() {
			function_arg_parser = Sequence([
				new() { ParserDirect = Many(Letter(), "fn_arg_type", 1).Map(x => string.Concat(x)), Optional = false, Key = "fn_arg_type" },
				new() { ParserDirect = Whitespace(), Optional = false },
				new() { ParserDirect = Many(Letter(), "fn_arg_name", 1).Map(x => string.Concat(x)), Optional = false, Key = "fn_arg_name" },
				new() { ParserDirect = Sequence([
					new() { ParserDirect = Whitespace(), Optional = false },
					new() { ParserDirect = Char('=', "fn_arg_default_marker"), Optional = false },
					new() { ParserDirect = Whitespace(), Optional = false },
					new() { ParserDirect = Many(Letter(), "fn_arg_default_value").Map(x => string.Concat(x)), Optional = false, Key = "fn_arg_default_value" },
				], "fn_arg_default_value").Map(x => x["fn_arg_default_value"]), Optional = true, Key = "fn_arg_default_value" },
				new() { ParserDirect = Whitespace(), Optional = false },
			], "fn_arg").Map(x => {
				var has_default_value = x.ContainsKey("fn_arg_default_value");

				return new FunctionArg() { Name = x["fn_arg_name"], DefaultValue = has_default_value ? x["fn_arg_default_value"] : null, HasDefaultValue = has_default_value, Type = x["fn_arg_type"] };
			});

			function_args_parser = Sequence([
				new() { ParserFunc = () => function_arg_parser, Optional = false, Key = "fn_args_first" },
				new() { ParserDirect = Many(Sequence([
					new() { ParserDirect = Whitespace(), Optional = true },
					new() { ParserDirect = Char(',', ""), Optional = false },
					new() { ParserDirect = Whitespace(), Optional = true },
					new() { ParserFunc = () => function_arg_parser, Optional = false, Key = "fn_args_rest" },
				], "").Map(x => x["fn_args_rest"]), ""), Optional = true, Key = "fn_args_rest" },
			], "fn_args").Map(x => {
				var list = new List<FunctionArg>() { x["fn_args_first"] };

				if (x.ContainsKey("fn_args_rest")) {
					var y = (List<object>)x["fn_args_rest"];
					list.AddRange(y.Cast<FunctionArg>());
				}

				return list;
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
				if (!x.ContainsKey("fn_args")) {
					x["fn_args"] = new List<FunctionArg>();
				}

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
				var result = DSLParser.Parse("pub fn main(int i) {  }");

				Dumpify.DumpExtensions.Dump(result);
			}
		}
	}
}
