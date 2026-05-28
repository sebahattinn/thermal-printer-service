// Integration testler gercek host'lar (WebApplicationFactory) ayaga kaldirir;
// paralel kosturmak background servislerin zamanlamasini bozar. Hepsi seri.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
