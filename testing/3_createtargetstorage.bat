@echo the build is %1 
@echo Creating the target Storage Account
az storage account create -g azcopiertest%1 -l westeurope --name azcptesttarget%1
