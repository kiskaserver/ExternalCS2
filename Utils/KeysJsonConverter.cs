using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2GameHelper.Utils
{
    public class KeysJsonConverter : JsonConverter<Keys>
    {
        public override Keys Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (Enum.TryParse<Keys>(value, out var result))
                    return result;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                // Поддержка числовых значений (на случай, если кто-то редактирует вручную)
                var value = reader.GetInt32();
                if (Enum.IsDefined(typeof(Keys), value))
                    return (Keys)value;
            }

            return Keys.None;
        }

        public override void Write(Utf8JsonWriter writer, Keys value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}