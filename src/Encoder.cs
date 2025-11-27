using System;
using System.Text;

namespace Vault;

public static class Encoder {
  private const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
  private const int BaseValue = 36;

  public static string Encode(long idNumber, int length = 8) {
    if (idNumber < 0) throw new ArgumentException("ID must be non-negative");
    if (idNumber == 0) return new string('0', length);

    var result = string.Empty;
    var value = idNumber;

    while (value > 0) {
      var rem = value % BaseValue;
      value /= BaseValue;
      result = Chars[(int)rem] + result;
    }

    return result.PadLeft(length, '0');
  }

  public static long Decode(string code) {
    return Convert.ToInt64(code.ToUpperInvariant(), BaseValue);
  }
}
