dotnet publish \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:TrimMode=Link \
  -p:PublishReadyToRun=false \
  -p:EnableCompressionInSingleFile=true \
  -p:ApplicationIcon=images/shrinkeng.ico

