FROM ubuntu:20.04
MAINTAINER Ridwan Shariffdeen <ridwan@comp.nus.edu.sg>
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y \
    apt-transport-https \
    apt-utils \
    git \
    nano \
    software-properties-common \
    vim \
    wget
    
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
 dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
 
RUN apt-get update && apt-get install -y \
    dotnet-runtime-5.0 \
    dotnet-sdk-5.0

RUN add-apt-repository -y ppa:deadsnakes/ppa
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y  --no-install-recommends --force-yes \
    bear \
    nuget \
    python3.8 \
    python3-pip
    
RUN python3.8 -m pip --disable-pip-version-check --no-cache-dir install gitpython
RUN python3.8 -m pip --disable-pip-version-check --no-cache-dir install PyGithub
RUN python3.8 -m pip --disable-pip-version-check --no-cache-dir install alive-progress