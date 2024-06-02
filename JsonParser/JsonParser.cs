using System.Globalization;
using static ParserCombinatorLib.Parser;

namespace ParserCombinatorLib {
	public static class JsonParser {
		private static Parser? json_element_parser = null;
		private static Parser? json_elements_parser = null;
		private static Parser? json_null_parser = null;
		private static Parser? json_true_parser = null;
		private static Parser? json_value_parser = null;
		private static Parser? json_false_parser = null;
		private static Parser? json_number_parser = null;
		private static Parser? json_character_parser = null;
		private static Parser? json_string_parser = null;
		private static Parser? json_object_member_parser = null;
		private static Parser? json_object_members_parser = null;
		private static Parser? json_object_parser = null;
		private static Parser? json_array_parser = null;

		static JsonParser() {
			json_null_parser = Literal("null", null, "literal_null");
			json_true_parser = Literal("true", true, "literal_true");
			json_false_parser = Literal("false", false, "literal_false");

			json_number_parser = Sequence([
				new () { ParserDirect = Char('-', "number_negative_sign"), Optional = true, Key = "number_negative_sign" },
				new () { ParserDirect = Any_of(['1','2','3','4','5','6','7','8','9'], "number_first_digit"), Optional = false, Key = "number_first_digit" },
				new() { ParserDirect = Many(Digit(), "number_rest_digits").Map(x => string.Concat(x)), Optional = true, Key = "number_rest_digits" },
				new() {
					ParserDirect = Sequence([
						new() { ParserDirect = Char('.', "number_fraction_delimiter"), Optional = false, Key = "number_fraction_delimiter" },
						new() { ParserDirect = Many(Digit(), "number_fraction_digits").Map(x => string.Concat(x)), Optional = false, Key = "number_fraction_digits" },
					], "number_fractional_part").Map(x => string.Concat(x)),
					Optional = true,
					Key = "number_fractional_part"
				},
				new() {
					ParserDirect = Sequence([
						new() { ParserDirect = Any_of(['e', 'E'], "number_exponent_delimiter"), Optional = false, Key = "number_exponent_delimiter" },
						new() { ParserDirect = Any_of(['+', '-'], "number_exponent_sign"), Optional = true, Key = "number_exponent_sign" },
						new() { ParserDirect = Many(Digit(), "number_exponent_digits").Map(x => string.Concat(x)), Optional = false, Key = "number_exponent_digits" },
					], "number_exponential_part").Map(x => string.Concat(x)),
					Optional = true,
					Key = "number_exponential_part"
				},
			], "number").Map(x => double.Parse(string.Concat(x.Values), CultureInfo.InvariantCulture));

			json_character_parser = Any([
				Satisfies(c => !char.IsControl(c) && c != '"' && c != '\\', "not_special"),
				Sequence([
					new() { ParserDirect = Char('\\', "escaped_start"), Optional = false, Key = "escaped_start" },
					new() { ParserDirect = Any([
						Char('"', "escaped_quote"),
						Char('\\', "escaped_backslash"),
						Char('/', "escaped_forwardslash"),
						Char('b', "escaped_backspace"),
						Char('f', "escaped_formfeed"),
						Char('n', "escaped_linefeed"),
						Char('r', "escaped_carriagereturn"),
						Sequence([
							new() { ParserDirect = Char('u', "escaped_unicode_start"), Optional = false, Key = "escaped_unicode_start" },
							new() { ParserDirect = Many(Satisfies(c => char.IsAsciiHexDigit(c), "escaped_unicode_hex_digit").Expect("Expected Hex-Digit"), "escaped_unicode_hex", 4, 4).Expect("Expected exactly 4 Hex-Digits"), Optional = false, Key = "escaped_unicode_hex" },
						], "escaped_unicode").Map(x => $"{x["escaped_unicode_start"]}{string.Concat(x["escaped_unicode_hex"])}"),
					], "escaped_any"), Optional = false, Key = "escaped_char" },
				], "escaped").Map(x => {
					return $"{x["escaped_start"]}{x["escaped_char"]}";
			})
			], "character");

			json_string_parser = Sequence([
				new() { ParserDirect = Char('"', "string_start"), Optional = false },
				new() { ParserFunc = () => Many(json_character_parser, "object_member_identifier").Map(x => string.Concat(x)), Optional = false, Key = "string" },
				new() { ParserDirect = Char('"', "string_end").Expect($"Expeceted '\"'"), Optional = false },
			], "string").Map(x => x["string"]);

			json_object_member_parser = Sequence([
				new() { ParserDirect = Whitespace(), Optional = true },
				new() { ParserFunc = () => json_string_parser.Expect("Expected Key"), Optional = false, Key = "object_member_key",
				},
				new() { ParserDirect = Whitespace(), Optional = true },
				new() { ParserDirect = Char(':', "object_member_delimiter").Expect($"Expeceted ':'"), Optional = false },
				new() { ParserFunc = () => json_element_parser, Optional = false, Key = "object_member_value" },
			], "object_member_value").Map(x => new KeyValuePair<string, dynamic?>(x["object_member_key"], x["object_member_value"]));

			json_object_members_parser = Sequence([
				new() { ParserFunc = () => json_object_member_parser, Optional = false, Key = "object_members_first" },
				new() { ParserDirect = Many(Sequence([
					new() { ParserDirect = Char(',', "object_members_delimiter"), Optional = false },
					new() { ParserFunc = () => json_object_member_parser, Optional = false, Key = "object_members_rest" },
				], "object_members_sequence").Map(x => x["object_members_rest"]), "object_members_many"), Optional = true, Key = "object_members" },

			], "object_members").Map(x => {
				var list = new List<KeyValuePair<string, dynamic?>>() { x["object_members_first"] };

				if (x.ContainsKey("object_members_rest")) {
					list.AddRange((x["object_members_rest"] as List<object>).Cast<KeyValuePair<string, dynamic?>>());
				}

				return list;

			});

			json_object_parser = Sequence([
				new() { ParserDirect = Char('{', "object_start"), Optional = false },
				new() { ParserFunc = () => json_object_members_parser, Optional = true, Key = "object_members" },
				new() { ParserDirect = Whitespace(), Optional = true },
				new() { ParserDirect = Char('}', "object_end").Expect($"Expeceted '}}'"), Optional = false },
			], "object").Map(x => {
				var object_members = x["object_members"];

				return new Dictionary<string, dynamic?>(object_members);
			});

			json_array_parser = Sequence([
				new() { ParserDirect = Char('[', "array_start"), Optional = false },
				new() { ParserFunc = () => json_elements_parser, Optional = true, Key = "array_elements" },
				new() { ParserDirect = Whitespace(), Optional = true },
				new() { ParserDirect = Char(']', "array_end").Expect($"Expeceted ']'"), Optional = false },
			], "array").Map(x => {
				var array_elements = x["array_elements"];

				return new List<dynamic?>(array_elements);
			});

			json_value_parser = Any([json_null_parser, json_false_parser, json_true_parser, json_number_parser, json_object_parser, json_array_parser, json_string_parser], "value_any");

			json_element_parser = Sequence([
				new() { ParserDirect = Whitespace(), Optional = true },
				new() { ParserFunc = () => json_value_parser, Optional = false, Key = "element_value" },
				new() { ParserDirect = Whitespace(), Optional = true },
			], "element").Map(x => x["element_value"]);

			json_elements_parser = Sequence([
				new() { ParserFunc = () => json_element_parser, Optional = false, Key = "elements_first" },
				new() { ParserDirect = Many(Sequence([
					new() { ParserDirect = Char(',', "elements_delimiter"), Optional = false },
					new() { ParserFunc = () => json_element_parser, Optional = false, Key = "element" },
				], "elements_sequence").Map(x => x["element"]), "elements_many"), Optional = true, Key = "elements_rest" },

			], "elements").Map(x => {
				var list = new List<dynamic?>() { x["elements_first"] };

				if (x.ContainsKey("elements_rest")) {
					list.AddRange((x["elements_rest"] as List<dynamic?>).Cast<dynamic?>());
				}

				return list;
			});
		}

		public static dynamic? Parse(string input) {
			var result = json_element_parser.Parse(new ParserInput() { String = input.Trim(), Index = 0 });

			if (result.Success) return result.Value;

			else throw new ParsingException();
		}
	}
}

