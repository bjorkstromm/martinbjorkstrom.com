#!/bin/bash

dotnet tool restore
dotnet cake --bootstrap
dotnet cake $@