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
					new () { Parser = Char('-', "number_negative_sign"), Optional = true, Include = true },
				new () { Parser = Any_of(['1','2','3','4','5','6','7','8','9'], "number_first_digit"), Optional = false, Include = true },
				new() { Parser = Many(Digit(), "number_after_first_digits").Map(x => string.Concat(x)), Optional = true, Include = true },
				new() {
					Parser = Sequence([
						new() { Parser = Char('.', "number_fraction_delimiter"), Optional = false, Include = true },
						new() { Parser = Many(Digit(), "number_fraction_digits").Map(x => string.Concat(x)), Optional = false, Include = true },
					], "number_fractional_part").Map(x => string.Concat(x)),
					Optional = true,
					Include = true
				},
				new() {
					Parser = Sequence([
						new() { Parser = Any_of(['e', 'E'], "number_exponent_delimiter"), Optional = false, Include = true },
						new() { Parser = Any_of(['+', '-'], "number_exponent_sign"), Optional = true, Include = true },
						new() { Parser = Many(Digit(), "number_exponent_digits").Map(x => string.Concat(x)), Optional = false, Include = true },
					], "number_exponential_part").Map(x => string.Concat(x)),
					Optional = true,
					Include = true
				},
			], "number").Map(x => double.Parse(string.Concat(x), CultureInfo.InvariantCulture));

			json_character_parser = Any([
				Satisfies(c => !char.IsControl(c) && c != '"' && c != '\\', "not_special"),
				Sequence([
					new() { Parser = Char('\\', "escaped_start"), Optional = false, Include = true },
					new() { Parser = Any([
						Char('"', "escaped_quote"),
						Char('\\', "escaped_backslash"),
						Char('/', "escaped_forwardslash"),
						Char('b', "escaped_backspace"),
						Char('f', "escaped_formfeed"),
						Char('n', "escaped_linefeed"),
						Char('r', "escaped_carriagereturn"),
						Sequence([
							new() { Parser = Char('u', "escaped_unicode_start"), Optional = false, Include = true },
							new() { Parser = Many(Satisfies(c => char.IsAsciiHexDigit(c), "escaped_unicode_hex_digit").Expect("Expected Hex-Digit"), "escaped_unicode_hex", 4, 4).Expect("Expected exactly 4 Hex-Digits"), Optional = false, Include = true },
						], "escaped_unicode").Map(x => $"{x[0]}{string.Concat(x[1])}"),
					], "escaped_any"), Optional = false, Include = true },
				], "escaped").Map(x => {
				if (x.Count == 1) {
					return x[0];
				} else {
					// Escaped Char
					return $"{x[0]}{x[1]}";
				}
			})
			], "character");

			json_string_parser = Sequence([
				new() { Parser = Char('"', "string_start"), Optional = false, Include = false },
				new() { ParserFunc = () => Many(json_character_parser, "object_member_identifier").Map(x => string.Concat(x)), Optional = false, Include = true },
				new() { Parser = Char('"', "string_end").Expect($"Expeceted '\"'"), Optional = false, Include = false },
			], "string").Map(x => x[0]);

			json_object_member_parser = Sequence([
				new() { Parser = Whitespace(), Optional = true, Include = false },
				new() { ParserFunc = () => json_string_parser.Expect("Expected Key"), Optional = true, Include = true,
				},
				new() { Parser = Whitespace(), Optional = true, Include = false },
				new() { Parser = Char(':', "object_member_delimiter").Expect($"Expeceted ':'"), Optional = false, Include = false },
				new() { ParserFunc = () => json_element_parser, Optional = false, Include = true },
			], "object_member").Map(x => new KeyValuePair<string, dynamic?>(x[0], x[1]));

			json_object_members_parser = Sequence([
				new() { ParserFunc = () => json_object_member_parser, Optional = false, Include = true },
				new() { Parser = Many(Sequence([
					new() { Parser = Char(',', "object_members_delimiter"), Optional = false, Include = false },
					new() { ParserFunc = () => json_object_member_parser, Optional = false, Include = true },
				], "object_members_sequence").Map(x => x[0]), "object_members_many"), Optional = true, Include = true },

			], "object_members").Map(x => {
				var list = new List<KeyValuePair<string, dynamic?>>() { x[0] };

				if (x.Count > 1) {
					list.AddRange((x[1] as List<object>).Cast<KeyValuePair<string, dynamic?>>());
				}

				return list;

			});

			json_object_parser = Sequence([
				new() { Parser = Char('{', "object_start"), Optional = false, Include = false },
				new() { ParserFunc = () => json_object_members_parser, Optional = true, Include = true },
				new() { Parser = Whitespace(), Optional = true, Include = false },
				new() { Parser = Char('}', "object_end").Expect($"Expeceted '}}'"), Optional = false, Include = false },
			], "object").Map(x => {
				if (x.Count > 0) {
					return new Dictionary<string, dynamic?>(x[0]);
				}
				// Empty Object
				else return new Dictionary<string, dynamic?>();
			});

			json_array_parser = Sequence([
				new() { Parser = Char('[', "array_start"), Optional = false, Include = false },
				new() { ParserFunc = () => json_elements_parser, Optional = true, Include = true },
				new() { Parser = Whitespace(), Optional = true, Include = false },
				new() { Parser = Char(']', "array_end").Expect($"Expeceted ']'"), Optional = false, Include = false },
			], "array").Map(x => {
				if (x.Count > 0) {
					return new List<dynamic?>(x[0]);
				}
				// Empty Array
				else return new List<dynamic?>();
			});

			json_value_parser = Any([json_null_parser, json_false_parser, json_true_parser, json_number_parser, json_object_parser, json_array_parser, json_string_parser], "value_any");

			json_element_parser = Sequence([
				new() { Parser = Whitespace(), Optional = true, Include = false },
				new() { ParserFunc = () => json_value_parser, Optional = false, Include = true },
				new() { Parser = Whitespace(), Optional = true, Include = false },
			], "element").Map(x => x[0]);

			json_elements_parser = Sequence([
				new() { ParserFunc = () => json_element_parser, Optional = false, Include = true },
				new() { Parser = Many(Sequence([
					new() { Parser = Char(',', "elements_delimiter"), Optional = false, Include = false },
					new() { ParserFunc = () => json_element_parser, Optional = false, Include = true },
				], "elements_sequence").Map(x => x[0]), "elements_many"), Optional = true, Include = true },

			], "elements").Map(x => {
				var list = new List<dynamic?>() { x[0] };

				if (x.Count > 1) {
					list.AddRange((x[1] as List<object>).Cast<dynamic?>());
				}

				return list;

			});
		}

		public static dynamic? Parse(string input) {
			var (success, result, remaining) = json_element_parser.Parse((input.Trim(), 0));

			if (success) return result;

			else throw new Exception();
		}
	}
}

