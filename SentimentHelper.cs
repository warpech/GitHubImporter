using edu.stanford.nlp.ling;
using edu.stanford.nlp.neural.rnn;
using edu.stanford.nlp.pipeline;
using edu.stanford.nlp.sentiment;
using edu.stanford.nlp.trees;
using edu.stanford.nlp.util;
using java.io;
using java.text;
using java.util;
using Starcounter;
using Starcounter.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GitHubImporter {
    public class StanfordNLPModelNotFoundExpection : Exception {
    }

    public class SentimentHelper {
        private string JarRoot;
        private StanfordCoreNLP pipeline;
        public string LastError;

        public SentimentHelper() {
            var CurDir = Application.Current.WorkingDirectory;
            //JarRoot = @"..\..\..\..\paket-files\nlp.stanford.edu\stanford-corenlp-full-2015-12-09\models";
            JarRoot = Path.Combine(CurDir, "stanford-corenlp-full-2015-12-09\\stanford-corenlp-3.6.0-models");

            if (Directory.Exists(JarRoot) == false) {
                LastError = typeof(StanfordNLPModelNotFoundExpection).Name;
                return;
            }

            // Annotation pipeline configuration
            var props = new java.util.Properties();
            props.setProperty("annotators", "tokenize, ssplit, parse, sentiment");
            //props.setProperty("ner.useSUTime", "0");

            Directory.SetCurrentDirectory(JarRoot);
            pipeline = new StanfordCoreNLP(props);
            Directory.SetCurrentDirectory(CurDir);
        }

        public float Analyze(string text) {
            var annotation = new edu.stanford.nlp.pipeline.Annotation(text);
            pipeline.annotate(annotation);

            List<int> sentiments = new List<int>();
            var sentences = annotation.get(new CoreAnnotations.SentencesAnnotation().getClass()) as ArrayList;

            foreach (CoreMap sentence in sentences) {
                Tree tree = sentence.get(new SentimentCoreAnnotations.SentimentAnnotatedTree().getClass()) as Tree;
                int sentiment = RNNCoreAnnotations.getPredictedClass(tree);
                sentiments.Add(sentiment);
            }

            if (sentiments.Count == 0) {
                return 2; //empty means neutral
            }
            else {
                return (float)sentiments.Average();
            }
        }
    }
}
