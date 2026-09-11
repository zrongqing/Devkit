# DevkitPrism client structure

- `Devkit` is the WPF/Prism application shell. Keep `App` startup, composition root, Views, ViewModels, and WPF-only exception handling here. Application-shell services are under `Devkit/Services`; WPF diagnostics belong in `Devkit/Services/Diagnostics`.
- `Devkit.Core` must remain free of WPF and application-service implementation dependencies. `Devkit.Core.UI` owns reusable WPF, Syncfusion, and MVVM infrastructure. `Devkit.Prism` owns only Prism/DryIoc adapters and extensions.
- Service contracts belong in `Services/Devkit.Services.Interfaces`; implementations belong in `Services/Devkit.Services`. Keep matching feature folders in both projects when an abstraction is required.
- Client logging is a cross-cutting application service: place `IClientLogger` in `Services/Devkit.Services.Interfaces/Logging`, and Serilog providers, configuration, and sinks in `Services/Devkit.Services/Logging`. Do not put Serilog implementations or file sinks in the WPF shell.
- Register application services in `App.ConfigureServices`. ViewModels access remote or local capabilities only through injected contracts; they must not create `HttpClient`, loggers, or service implementations directly.
