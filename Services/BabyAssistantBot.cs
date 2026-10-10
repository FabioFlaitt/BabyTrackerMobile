using System;

namespace BabyTrackerMobile.Services
{
    /// <summary>
    /// Assistente simples (por regras) que responde dúvidas comuns sobre o bebê.
    /// Não usa internet nem IA externa, então não tem custo nem chave secreta.
    /// Sempre reforça que não substitui o pediatra.
    /// </summary>
    public static class BabyAssistantBot
    {
        public const string Nickname = "🤖 Assistente";

        private const string Disclaimer = "\n\n⚠️ Isto é orientação geral e não substitui o pediatra.";

        /// <summary>Retorna uma resposta se a mensagem casar com algum tema; senão null.</summary>
        public static string? TryAnswer(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return null;
            var m = Normalize(message);

            // Saudações
            if (Has(m, "oi", "ola", "bom dia", "boa tarde", "boa noite", "ajuda", "help"))
                return "Oi! 👋 Posso ajudar com dúvidas gerais sobre febre, mamada, sono, fralda, choro, cólica e vacina. É só perguntar." ;

            if (Has(m, "febre", "temperatura", "quente"))
                return "🌡️ Febre em bebê é temperatura ≥ 37,8°C (axilar). Em menores de 3 meses, qualquer febre pede avaliação médica no mesmo dia. Mantenha hidratado e observe. Procure emergência se houver dificuldade para respirar, moleza intensa ou manchas na pele." + Disclaimer;

            if (Has(m, "mamada", "amament", "leite", "mamar", "peito", "mamadeira"))
                return "🍼 Recém-nascido costuma mamar 8–12 vezes por dia (a cada 2–3h), inclusive à noite. Sinais de boa ingestão: 6+ fraldas molhadas/dia e ganho de peso. Ofereça sempre que der sinais de fome (OMS/AAP)." + Disclaimer;

            if (Has(m, "sono", "dormir", "soneca", "dorme"))
                return "😴 Recém-nascido dorme 14–17h por dia (NSF/AAP), em vários períodos curtos. Deite-o sempre de barriga para cima, no berço firme, sem travesseiros ou objetos soltos, para reduzir o risco de morte súbita." + Disclaimer;

            if (Has(m, "fralda", "xixi", "coco", "cocô", "evacu", "fezes"))
                return "🧷 Nos primeiros dias: 6+ fraldas de xixi/dia indicam boa hidratação. O cocô muda de mecônio (escuro) para amarelado. ⚠️ Fezes muito claras/esbranquiçadas ou com sangue = procure o pediatra." + Disclaimer;

            if (Has(m, "colica", "cólica", "gases", "chora muito", "choro"))
                return "😣 Cólica é comum até ~3–4 meses. Ajuda: colo, movimento suave, compressa morna na barriga, massagem e arrotar após mamar. Se o choro vier com febre, vômito ou recusa alimentar, procure avaliação." + Disclaimer;

            if (Has(m, "vacina", "vacinar", "imuniz"))
                return "💉 Siga o calendário do PNI/SBP. Ao nascer: BCG e Hepatite B. Depois há doses aos 2, 4, 6 meses e seguintes. Guarde a caderneta e confirme as datas com a UBS ou o pediatra." + Disclaimer;

            if (Has(m, "peso", "engordar", "emagrec"))
                return "⚖️ É normal o recém-nascido perder até ~7–10% do peso nos primeiros dias e recuperar até ~15 dias de vida. O acompanhamento do peso é feito pelo pediatra na consulta de puericultura." + Disclaimer;

            if (Has(m, "banho", "umbigo", "coto"))
                return "🛁 O coto umbilical cai entre 1–3 semanas. Mantenha limpo e seco. Pode dar banho normalmente; seque bem a região. ⚠️ Vermelhidão, mau cheiro ou secreção com pus = avalie com o pediatra." + Disclaimer;

            return null; // sem resposta pronta para este assunto
        }

        private static bool Has(string m, params string[] terms)
        {
            foreach (var t in terms)
                if (m.Contains(t)) return true;
            return false;
        }

        private static string Normalize(string s)
            => s.Trim().ToLowerInvariant();
    }
}
