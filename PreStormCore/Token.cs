using System;
using System.Threading.Tasks;

namespace PreStormCore
{
    internal class Token
    {
        private string? token;
        private DateTime expiry;
        private readonly Func<Token>? generateToken;

        public event EventHandler? TokenGenerated;

        private Token(string token, DateTime expiry)
        {
            this.token = token;
            this.expiry = expiry;
        }

        public Token(string token)
            : this(token, DateTime.MaxValue)
        {
        }

        public Token(Func<Token> generateToken)
        {
            this.generateToken = generateToken;
        }

        public Token(string tokenUrl, string userName, string password)
            : this(() => GenerateToken(tokenUrl, userName, password).Result)
        {
        }

        public static async Task<Token> GenerateToken(string tokenUrl, string userName, string password, int expiration = 60)
        {
            var token = await Esri.GetTokenInfo(tokenUrl, userName, password, expiration);
            return new Token(token.token, Esri.BaseTime.AddMilliseconds(token.expires));
        }

        public double MinutesRemaining => expiry.Subtract(DateTime.UtcNow).TotalMinutes;

        public override string ToString()
        {
            if (generateToken is not null && MinutesRemaining < 0.5)
            {
                var token = generateToken();
                this.token = token.token;
                expiry = token.expiry;

                TokenGenerated?.Invoke(this, EventArgs.Empty);
            }

            return token!;
        }
    }
}
