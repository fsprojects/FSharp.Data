FROM gitpod/workspace-dotnet

USER root

# Set up shell
RUN apt-get update && apt-get install -yq zsh
RUN sh -c "$(curl -fsSL https://raw.github.com/ohmyzsh/ohmyzsh/master/tools/install.sh)"
ENV SHELL=zsh
