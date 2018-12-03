# XTMF

XTMF is software that creates, edits and runs _model systems_. Model systems in XTMF are designed with a hierarchical compositions of modules. Every module in a model system provides data input and custom execution logic. XTMF uses projects as an organizational structure for containing a collection of model systems.

XTMF and its modules are written in C# using the .NET framework.

A large collection of modules are available as part of this repository - and is also included with the binary releases of XTMF.

The current module library largely supports the creation of travel-demand related model systems. Many of the modules included with XTMF are part of Travel Modelling Group's GTAModel. Model systems need not be limited to the transit domain, but will most likely require the development of your own custom modules.

## Extensible Development

XTMF includes an SDK for the development of your own custom modules. Custom modules are written in C# that provide custom executable logic and pre-defined module information (such as input properties).

A guide for writing your own XTMF modules is available [here](https://tmg.utoronto.ca/documentation/Documentation/1.4_docfx/_site/articles/programming/modules.html 'Writing Custom Modules').

## Installation

The most recent XTMF binary release is available on the [releases](https://github.com/TravelModellingGroup/XTMF/releases 'XTMF Releases') page.

### To Run XTMF

1. Download the .zip archive of XTMF version of your choosing.
2. Extract the .zip archive.
3. Run XTMF.Gui.exe to launch XTMF with a graphical user interface. XTMF.Run.exe can be used to run XTMF in headless mode.

## Development

For development of XTMF, Visual Studio 2017 or later is required. _XTMF.sln_ in the _Code/_ folder is the solution file that should be used.

**Note:** Module development does not require using Visual Studio as your IDE. Rider, Visual Studio Code and others are suitable alternatives for module development _only_ with the XTMF SDK.

## Documentation

Documentation for XTMF is available on the Travel Modelling Group's [documentation site](https://tmg.utoronto.ca/doc 'XTMF User Guide').

## Licensing

The eXtensible Travel Modelling Framework

Licensed under the GPLv3 which is available in the root directory under "License".

The eXtensible Travel Modelling Framework (XTMF) is an open-source (GPLv3) software platform developed by the University of Toronto to build model systems through the composition and configuration of different modules. This means that all modules used to construct GTAModel V4.0 have their code available for inspection or recompilation