#!/bin/bash
fsharpi build.fsx
git checkout gh-pages
cp ../docs/experimental/*.html ../experimental/
cp ../docs/library/*.html ../library/
cp ../docs/tutorials/*.html ../tutorials/
cp ../docs/*.html ../
git commit -a -m "Update generated documentation"
git push
git checkout master