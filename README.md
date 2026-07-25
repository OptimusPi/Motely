# Motely

Motely is a fast Balatro seed searching library. It uses your CPU's 512-bit registers with SIMD to search 8 seeds at once per thread.
It performs very well compared to current GPU-based Balatro seed searches (better on a lot of systems), and is the fastest general-purpose CPU-based searcher I know of.

Filters are written in JAML (Jimbo's Ante Markup Language). The C# engine owns the grammar: load a filter and it becomes a typed `JamlConfig` the search runs. Clauses can be one-line natural language ("Blueprint in ante 1") or structured documents — both are JAML.

Thank you so much to [@OptimusPi](https://github.com/OptimusPi/) for commissioning the development of this library. It started out as a personal project, and their support is what gave it the capabilities it has today.
