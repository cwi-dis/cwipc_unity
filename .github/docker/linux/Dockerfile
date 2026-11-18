# game-ci's Unity image with cwipc added, for the EditMode tests in a Linux container
# (cwi-dis/cwipc_unity#71). Built in the CI job and passed to game-ci/unity-test-runner
# as customImage.
#
# UNITY_VERSION: the project's Unity version (cwipc_unity_develop/ProjectSettings/ProjectVersion.txt).
# CWIPC_VERSION: a cwipc release tag, or "nightly". The -built- archive of that release is
# installed with cwipc's get-cwipc.sh, with its runtime dependencies (apt), without Python.

# Image version 4 (game-ci/docker v4.0.0) is Ubuntu 24.04, as cwipc's Linux archive needs;
# version 3 is Ubuntu 22.04. Version 4 images exist only for recent Unity versions
# (for example 6000.3.26f1, not 6000.3.10f1).
ARG UNITY_VERSION
ARG GAMECI_IMAGE_VERSION=4
FROM unityci/editor:ubuntu-${UNITY_VERSION}-linux-il2cpp-${GAMECI_IMAGE_VERSION}

ARG CWIPC_VERSION=nightly

# Workarounds, blocked on cwi-dis/cwipc#383: cwipc's install-deps-ubuntu2404.sh calls sudo
# unconditionally, also as root, and this image has no sudo; and it installs udev rules
# for Orbbec cameras into /etc/udev/rules.d, which doesn't exist in a container. Remove
# both once fixed.
RUN apt-get update \
    && apt-get install -y --no-install-recommends sudo curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /etc/udev/rules.d

RUN curl -sSfL "https://raw.githubusercontent.com/cwi-dis/cwipc/${CWIPC_VERSION}/scripts/get-cwipc.sh" -o /tmp/get-cwipc.sh \
    && bash /tmp/get-cwipc.sh --method archive --deps --no-python "${CWIPC_VERSION}" /opt/cwipc \
    && rm -f /tmp/get-cwipc.sh \
    && rm -rf /var/lib/apt/lists/*

# Let the dynamic loader find cwipc's libraries without LD_LIBRARY_PATH: game-ci passes only
# a fixed set of environment variables into the container.
RUN echo /opt/cwipc/lib > /etc/ld.so.conf.d/cwipc.conf && ldconfig
