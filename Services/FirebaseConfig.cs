namespace BabyTrackerMobile.Services
{
    /// <summary>
    /// Configuração pública do Firebase (segura para ficar no app — são as mesmas
    /// chaves de cliente do google-services). Preencha os 3 valores abaixo com os
    /// dados do seu projeto no console.firebase.google.com:
    ///   - ApiKey:     Configurações do projeto → Geral → "Chave de API da Web"
    ///   - ProjectId:  Configurações do projeto → Geral → "ID do projeto"
    ///   - DatabaseUrl: Realtime Database → a URL no topo
    ///                  (ex: https://SEU-PROJETO-default-rtdb.firebaseio.com)
    /// </summary>
    public static class FirebaseConfig
    {
        public const string ApiKey = "COLE_AQUI_A_WEB_API_KEY";
        public const string ProjectId = "COLE_AQUI_O_PROJECT_ID";
        public const string DatabaseUrl = "https://COLE-AQUI-default-rtdb.firebaseio.com";

        /// <summary>true quando as chaves já foram preenchidas (não são mais placeholders).</summary>
        public static bool IsConfigured =>
            !ApiKey.StartsWith("COLE_AQUI")
            && !DatabaseUrl.Contains("COLE-AQUI")
            && !ProjectId.StartsWith("COLE_AQUI");
    }
}
