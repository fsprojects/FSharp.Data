#!/bin/bash

# To use this script:
#  - Make sure you have `fsharpi` in the PATH
#  - Make sure you do not have any pending changes in `git status`
#  - Go to the 'tools' directory and run `./update-docs.sh`

fsharpi build.fsx
git checkout gh-pages
cp ../docs/experimental/*.html ../experimental/
cp ../docs/library/*.html ../library/
cp ../docs/tutorials/*.html ../tutorials/
cp ../docs/*.html ../
git commit -a -m "Update generated documentation"
git push
git checkout master