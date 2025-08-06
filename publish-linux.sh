#!/bin/bash
dotnet publish \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:TrimMode=Link \
  -p:PublishReadyToRun=false \
  -p:EnableCompressionInSingleFile=true