namespace StockGraph.Core;

public static class Vocabulary
{
    public const string Base = "https://stockgraph.local/kg/";
    public const string Ex = Base;
    public const string Schema = "https://schema.org/";
    public const string Xsd = "http://www.w3.org/2001/XMLSchema#";
    public const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    public const string Rdfs = "http://www.w3.org/2000/01/rdf-schema#";
    public const string GraphIri = Base + "graph/main";

    public static Uri Iri(string value) => new(value);
    public static Uri ExTerm(string term) => Iri(Ex + term);
    public static Uri SchemaTerm(string term) => Iri(Schema + term);
    public static Uri XsdTerm(string term) => Iri(Xsd + term);
    public static Uri RdfTerm(string term) => Iri(Rdf + term);
    public static Uri RdfsTerm(string term) => Iri(Rdfs + term);
}