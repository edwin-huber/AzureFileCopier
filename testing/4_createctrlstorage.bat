@echo the build is %1 
@echo Creating the control Storage Account
az storage account create -g azcopiertest%1 -l westeurope --name azctrltesttarget%1
